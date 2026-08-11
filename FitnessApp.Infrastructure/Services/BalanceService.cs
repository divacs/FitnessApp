using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Memberships.Mappings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

/// <summary>
/// Implements package creation, single-session management, carry-over, and balance consumption rules.
/// </summary>
public class BalanceService : IBalanceService
{
    private const int MaxCarriedOverSessions = 2;

    private readonly AppDbContext _dbContext;
    private readonly ILogger<BalanceService> _logger;

    public BalanceService(
        AppDbContext dbContext,
        ILogger<BalanceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CurrentBalanceResponse> GetCurrentBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var utcNow = DateTime.UtcNow;

        var activePackage = await GetAvailableMonthlyPackagesQuery(userId, utcNow)
            .AsNoTracking()
            .OrderBy(balance => balance.EndDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePackage is null)
        {
            activePackage = await _dbContext.UserTrainingBalances
                .AsNoTracking()
                .Where(balance =>
                    balance.UserId == userId
                    && balance.PurchaseType == PurchaseType.SingleSessions
                    && balance.IsActive
                    && !balance.IsExpired
                    && balance.RemainingSessions > 0
                    && _dbContext.Payments.Any(payment =>
                        payment.UserId == balance.UserId
                        && payment.PaymentType == balance.PurchaseType))
                .OrderByDescending(balance => balance.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var singleSessionsRemaining = await GetAvailableSingleSessionsQuery(userId)
            .AsNoTracking()
            .Where(balance =>
                _dbContext.Payments.Any(payment =>
                    payment.UserId == balance.UserId
                    && payment.PaymentType == balance.PurchaseType))
            .SumAsync(balance => balance.RemainingSessions, cancellationToken);

        if (activePackage is not null && activePackage.PurchaseType == PurchaseType.SingleSessions)
        {
            singleSessionsRemaining -= activePackage.RemainingSessions;
        }

        var activePackageRemainingSessions = activePackage?.RemainingSessions ?? 0;
        var totalRemainingSessions = activePackageRemainingSessions + singleSessionsRemaining;

        return new CurrentBalanceResponse
        {
            ActivePackage = activePackage?.ToResponse(),
            SingleSessionsRemaining = singleSessionsRemaining,
            TotalRemainingSessions = totalRemainingSessions,
            HasAvailableSessions = totalRemainingSessions > 0,
            MembershipExpiresAt = activePackage?.EndDate
        };
    }

    public async Task<IReadOnlyCollection<BalanceHistoryResponse>> GetBalanceHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var balances = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.UserId == userId
                && (
                    balance.PurchaseType == PurchaseType.SingleSessions
                        ? _dbContext.Payments.Any(payment =>
                            payment.UserId == balance.UserId
                            && payment.PaymentType == balance.PurchaseType)
                        : balance.PurchaseType != PurchaseType.Package6
                            && balance.PurchaseType != PurchaseType.Package12
                            && balance.PurchaseType != PurchaseType.Package16
                            || _dbContext.Payments.Any(payment =>
                                payment.UserId == balance.UserId
                                && payment.PaymentType == balance.PurchaseType
                                && payment.StartDate == balance.StartDate)))
            .OrderByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        return balances
            .Select(balance => balance.ToHistoryResponse())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<MembershipHistoryResponse>> GetMembershipHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var activeMembershipIds = await GetAvailableMonthlyPackagesQuery(userId, utcNow)
            .AsNoTracking()
            .Select(balance => balance.Id)
            .ToListAsync(cancellationToken);

        var memberships = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.UserId == userId
                && (balance.PurchaseType == PurchaseType.Package6
                    || balance.PurchaseType == PurchaseType.Package12
                    || balance.PurchaseType == PurchaseType.Package16)
                && _dbContext.Payments.Any(payment =>
                    payment.UserId == balance.UserId
                    && payment.PaymentType == balance.PurchaseType
                    && payment.StartDate == balance.StartDate))
            .OrderByDescending(balance => balance.StartDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        var paymentDates = await _dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.UserId == userId
                && (payment.PaymentType == PurchaseType.Package6
                    || payment.PaymentType == PurchaseType.Package12
                    || payment.PaymentType == PurchaseType.Package16))
            .OrderByDescending(payment => payment.PaymentDate)
            .Select(payment => new MembershipPaymentLookup(
                payment.PaymentType,
                payment.StartDate,
                payment.PaymentDate,
                payment.NumberOfSessions))
            .ToListAsync(cancellationToken);

        var singleSessionPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.UserId == userId
                && payment.PaymentType == PurchaseType.SingleSessions)
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

        return memberships
            .Select(balance => balance.ToMembershipHistoryResponse(
                FindPaymentDate(balance, paymentDates),
                activeMembershipIds.Contains(balance.Id)))
            .Concat(singleSessionPayments.Select(payment => new MembershipHistoryResponse
            {
                Id = payment.Id,
                PurchaseType = payment.PaymentType,
                PackageName = "Pojedinačni termini",
                StartDate = payment.PaymentDate,
                PaymentDate = payment.PaymentDate,
                EndDate = null,
                TotalSessions = payment.NumberOfSessions,
                RemainingSessions = payment.NumberOfSessions,
                IsCurrentlyActive = false
            }))
            .OrderByDescending(membership => membership.PaymentDate)
            .ThenByDescending(membership => membership.StartDate)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<UserTrainingBalanceResponse>> GetUserBalancesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var balances = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.UserId == userId
                && (
                    balance.PurchaseType == PurchaseType.SingleSessions
                        ? _dbContext.Payments.Any(payment =>
                            payment.UserId == balance.UserId
                            && payment.PaymentType == balance.PurchaseType)
                        : balance.PurchaseType != PurchaseType.Package6
                            && balance.PurchaseType != PurchaseType.Package12
                            && balance.PurchaseType != PurchaseType.Package16
                            || _dbContext.Payments.Any(payment =>
                                payment.UserId == balance.UserId
                                && payment.PaymentType == balance.PurchaseType
                                && payment.StartDate == balance.StartDate)))
            .OrderByDescending(balance => balance.StartDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        return balances
            .Select(balance => balance.ToResponse())
            .ToArray();
    }

    public Task<IReadOnlyCollection<AvailablePackageResponse>> GetAvailablePackagesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<AvailablePackageResponse> packages =
        [
            new AvailablePackageResponse
            {
                Title = "Paket 6 termina",
                Subtitle = "Za fleksibilan ritam treninga",
                Description = "Paket važi 30 dana od dana aktivacije.",
                LearnMoreDescription = "Za one koji žele da treniraju povremeno i zadrže slobodu u rasporedu.",
                Details =
                [
                    "Šta uključuje: 6 grupnih treninga u toku meseca.",
                    "Trajanje paketa: Paket važi 30 dana od dana aktivacije.",
                    "Korišćenje termina: Termini se zakazuju unapred putem aplikacije. Na raspolaganju su do 3 treninga nedeljno.",
                    "Prenos termina: Neiskorišćeni termini se ne prenose u naredni mesec.",
                    "Cena: Na upit."
                ],
                Badge = null,
                Features =
                [
                    "6 grupnih treninga u toku meseca",
                    "Do 3 treninga nedeljno",
                    "Bez prenosa neiskorišćenih termina"
                ],
                Price = "Na upit",
                PurchaseType = PurchaseType.Package6,
                NumberOfSessions = 6
            },
            new AvailablePackageResponse
            {
                Title = "Paket 12 termina",
                Subtitle = "Najpopularniji izbor",
                Description = "Paket važi 30 dana od dana aktivacije.",
                LearnMoreDescription = "Idealna ravnoteža između kontinuiteta i fleksibilnosti.",
                Details =
                [
                    "Šta uključuje: 12 grupnih treninga u toku meseca.",
                    "Trajanje paketa: Paket važi 30 dana od dana aktivacije.",
                    "Korišćenje termina: Termini se zakazuju unapred putem aplikacije. Možete trenirati do 3 puta nedeljno.",
                    "Prenos termina: Do 2 neiskorišćena termina mogu se preneti u naredni mesec uz obnovu paketa.",
                    "Cena: Na upit."
                ],
                Badge = "Najpopularniji izbor",
                Features =
                [
                    "12 grupnih treninga u toku meseca",
                    "Do 3 treninga nedeljno",
                    "Do 2 preneta termina uz obnovu"
                ],
                Price = "Na upit",
                PurchaseType = PurchaseType.Package12,
                NumberOfSessions = 12
            },
            // new AvailablePackageResponse
            // {
            //     Title = "Paket 16 termina",
            //     Subtitle = "Za maksimalne rezultate.",
            //     Description = "Paket važi 30 dana od dana aktivacije.",
            //     LearnMoreDescription = "Za korisnike kojima je važan redovan ritam i veći broj treninga.",
            //     Details =
            //     [
            //         "Šta uključuje: 16 grupnih treninga u toku meseca.",
            //         "Trajanje paketa: Paket važi 30 dana od dana aktivacije.",
            //         "Korišćenje termina: Paket je namenjen redovnom treniranju i omogućava do 3 treninga nedeljno.",
            //         "Prenos termina: Do 2 neiskorišćena termina mogu se preneti u naredni mesec uz obnovu paketa.",
            //         "Cena: Na upit."
            //     ],
            //     Badge = null,
            //     Features =
            //     [
            //         "16 grupnih treninga u toku meseca",
            //         "Do 3 treninga nedeljno",
            //         "Do 2 preneta termina uz obnovu"
            //     ],
            //     Price = "Na upit",
            //     PurchaseType = PurchaseType.Package16,
            //     NumberOfSessions = 16
            // },
            new AvailablePackageResponse
            {
                Title = "Pojedinačni trening",
                Subtitle = "Fleksibilna opcija bez članarine",
                Description = "Trening se može iskoristiti u roku od 30 dana od kupovine.",
                LearnMoreDescription = "Trenirajte kada vam odgovara, bez mesečne obaveze.",
                Details =
                [
                    "Šta uključuje: 1 grupni trening.",
                    "Trajanje: Trening se može iskoristiti u roku od 30 dana od kupovine.",
                    "Fleksibilnost: Namenjen je svima koji ne žele mesečnu obavezu i žele da treniraju kada im raspored dozvoli.",
                    "Zakazivanje: Termin se rezerviše unapred putem aplikacije.",
                    "Cena: Na upit."
                ],
                Badge = null,
                Features =
                [
                    "1 grupni trening",
                    "Bez mesečne obaveze",
                    "Rezervacija unapred putem aplikacije"
                ],
                Price = "Na upit",
                PurchaseType = PurchaseType.SingleSessions,
                NumberOfSessions = 1
            }
        ];

        return Task.FromResult(packages);
    }

    public Task<UserTrainingBalanceResponse> CreatePackage12Async(
        Guid userId,
        CreatePackage12Request request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        return CreateMonthlyPackageAsync(
            userId,
            request.StartDate,
            request.Notes,
            adminId,
            PurchaseType.Package12,
            totalSessions: 12,
            cancellationToken);
    }

    public Task<UserTrainingBalanceResponse> CreatePackage6Async(
        Guid userId,
        CreatePackage6Request request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        return CreateMonthlyPackageAsync(
            userId,
            request.StartDate,
            request.Notes,
            adminId,
            PurchaseType.Package6,
            totalSessions: 6,
            cancellationToken);
    }

    public Task<UserTrainingBalanceResponse> CreatePackage16Async(
        Guid userId,
        CreatePackage16Request request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        return CreateMonthlyPackageAsync(
            userId,
            request.StartDate,
            request.Notes,
            adminId,
            PurchaseType.Package16,
            totalSessions: 16,
            cancellationToken);
    }

    public Task<UserTrainingBalanceResponse> AddSingleSessionsAsync(
        Guid userId,
        AddSingleSessionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        return AddSingleSessionsInternalAsync(userId, request, adminId, cancellationToken);
    }

    public async Task ApplyCarryOverAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var package12Balances = await _dbContext.UserTrainingBalances
            .Where(balance =>
                balance.UserId == userId
                && balance.PurchaseType == PurchaseType.Package12)
            .OrderByDescending(balance => balance.StartDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (package12Balances.Count < 2)
        {
            _logger.LogInformation(
                "Carry-over skipped for user {UserId} because there is no previous Package12 balance.",
                userId);
            return;
        }

        var newPackage = package12Balances[0];
        var previousPackage = package12Balances[1];

        if (previousPackage.EndDate > DateTime.UtcNow)
        {
            _logger.LogInformation(
                "Carry-over skipped from Package12 balance {PreviousBalanceId} because the package is still active.",
                previousPackage.Id);
            return;
        }

        if (newPackage.CarriedOverSessions > 0)
        {
            _logger.LogInformation(
                "Carry-over skipped for Package12 balance {BalanceId} because it already has {CarriedOverSessions} carried sessions.",
                newPackage.Id,
                newPackage.CarriedOverSessions);
            return;
        }

        if (previousPackage.RemainingSessions <= 0)
        {
            _logger.LogInformation(
                "Carry-over skipped from Package12 balance {PreviousBalanceId} because there are no remaining sessions.",
                previousPackage.Id);
            return;
        }

        // Carry-over is limited to the immediately previous Package12 and can transfer at most two unused sessions.
        var carriedOverSessions = Math.Min(previousPackage.RemainingSessions, MaxCarriedOverSessions);

        newPackage.TotalSessions += carriedOverSessions;
        newPackage.RemainingSessions += carriedOverSessions;
        newPackage.CarriedOverSessions = carriedOverSessions;
        newPackage.UpdatedAt = DateTime.UtcNow;

        previousPackage.IsExpired = true;
        previousPackage.IsActive = false;
        previousPackage.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Carried over {CarriedOverSessions} sessions from Package12 balance {PreviousBalanceId} to Package12 balance {NewBalanceId} for user {UserId}.",
            carriedOverSessions,
            previousPackage.Id,
            newPackage.Id,
            userId);
    }

    public async Task ConsumeSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var balance = await GetAvailableMonthlyPackagesQuery(userId, utcNow)
            .OrderBy(balance => balance.EndDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // ATTENDED and NO_SHOW consume from an active monthly package first, then fall back to single sessions.
        balance ??= await GetAvailableSingleSessionsQuery(userId)
            .OrderBy(balance => balance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (balance is null)
        {
            _logger.LogWarning("Unable to consume session for user {UserId} because no sessions are available.", userId);
            throw new ConflictException("Korisnik nema dostupnih termina.");
        }

        balance.RemainingSessions -= 1;
        balance.UpdatedAt = DateTime.UtcNow;

        if (balance.RemainingSessions <= 0)
        {
            balance.IsActive = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Consumed one session from balance {BalanceId} for user {UserId}. Remaining sessions: {RemainingSessions}.",
            balance.Id,
            userId,
            balance.RemainingSessions);
    }

    public Task<UserTrainingBalanceResponse> UpdateBalanceAsync(
        Guid balanceId,
        UpdateBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new BadRequestException("Ažuriranje stanja termina biće implementirano u narednom koraku.");
    }

    public Task DeleteBalanceAsync(
        Guid balanceId,
        CancellationToken cancellationToken = default)
    {
        throw new BadRequestException("Brisanje stanja termina biće implementirano u narednom koraku.");
    }

    private async Task EnsureUserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (!userExists)
        {
            _logger.LogWarning("Balance requested for missing user {UserId}.", userId);
            throw new NotFoundException("Korisnik nije pronađen.");
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new BadRequestException("Korisnik je obavezan.");
        }
    }

    private static void ValidateStartDate(DateTime startDate)
    {
        if (startDate == default)
        {
            throw new BadRequestException("Datum početka je obavezan.");
        }
    }

    private IQueryable<UserTrainingBalance> GetAvailableMonthlyPackagesQuery(
        Guid userId,
        DateTime utcNow)
    {
        return _dbContext.UserTrainingBalances
            .Where(balance =>
                balance.UserId == userId
                && balance.IsActive
                && !balance.IsExpired
                && balance.RemainingSessions > 0
                && (balance.PurchaseType == PurchaseType.Package6
                    || balance.PurchaseType == PurchaseType.Package12
                    || balance.PurchaseType == PurchaseType.Package16)
                && balance.StartDate <= utcNow
                && balance.EndDate >= utcNow
                && _dbContext.Payments.Any(payment =>
                    payment.UserId == balance.UserId
                    && payment.PaymentType == balance.PurchaseType
                    && payment.StartDate == balance.StartDate));
    }

    private IQueryable<UserTrainingBalance> GetAvailableSingleSessionsQuery(Guid userId)
    {
        return _dbContext.UserTrainingBalances
            .Where(balance =>
                balance.UserId == userId
                && balance.PurchaseType == PurchaseType.SingleSessions
                && balance.IsActive
                && !balance.IsExpired
                && balance.RemainingSessions > 0);
    }

    private async Task<UserTrainingBalanceResponse> CreateMonthlyPackageAsync(
        Guid userId,
        DateTime startDate,
        string? notes,
        Guid adminId,
        PurchaseType purchaseType,
        int totalSessions,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);
        ValidateStartDate(startDate);
        await EnsureUserExistsAsync(userId, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var hasActiveSamePackage = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .AnyAsync(
                balance =>
                    balance.UserId == userId
                    && balance.PurchaseType == purchaseType
                    && balance.IsActive
                    && !balance.IsExpired,
                cancellationToken);

        if (hasActiveSamePackage)
        {
            _logger.LogInformation(
                "User {UserId} already has an active {PurchaseType} package. Creating another package.",
                userId,
                purchaseType);
        }

        var effectiveStartDate = await ResolveNewMonthlyPackageStartDateAsync(
            userId,
            startDate,
            utcNow,
            cancellationToken);

        var balance = new UserTrainingBalance
        {
            UserId = userId,
            PurchaseType = purchaseType,
            TotalSessions = totalSessions,
            RemainingSessions = totalSessions,
            StartDate = effectiveStartDate,
            EndDate = effectiveStartDate.AddMonths(1),
            IsActive = true,
            IsExpired = false,
            CreatedByAdminId = adminId,
            CreatedAt = utcNow,
            Notes = notes
        };

        _dbContext.UserTrainingBalances.Add(balance);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (purchaseType == PurchaseType.Package12)
        {
            await ApplyCarryOverAsync(userId, cancellationToken);
        }

        _logger.LogInformation(
            "Created {PurchaseType} balance {BalanceId} for user {UserId} by admin {AdminId}.",
            purchaseType,
            balance.Id,
            userId,
            adminId);

        return balance.ToResponse();
    }

    private async Task<UserTrainingBalanceResponse> AddSingleSessionsInternalAsync(
        Guid userId,
        AddSingleSessionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);

        if (request.NumberOfSessions <= 0)
        {
            throw new BadRequestException("Broj termina mora biti veći od 0.");
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        var activeSingleSessionsBalance = await _dbContext.UserTrainingBalances
            .FirstOrDefaultAsync(
                balance =>
                    balance.UserId == userId
                    && balance.PurchaseType == PurchaseType.SingleSessions
                    && balance.IsActive
                    && !balance.IsExpired,
                cancellationToken);

        if (activeSingleSessionsBalance is not null)
        {
            activeSingleSessionsBalance.TotalSessions += request.NumberOfSessions;
            activeSingleSessionsBalance.RemainingSessions += request.NumberOfSessions;
            activeSingleSessionsBalance.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                activeSingleSessionsBalance.Notes = request.Notes;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Added {NumberOfSessions} single sessions to balance {BalanceId} for user {UserId} by admin {AdminId}.",
                request.NumberOfSessions,
                activeSingleSessionsBalance.Id,
                userId,
                adminId);

            return activeSingleSessionsBalance.ToResponse();
        }

        var balance = new UserTrainingBalance
        {
            UserId = userId,
            PurchaseType = PurchaseType.SingleSessions,
            TotalSessions = request.NumberOfSessions,
            RemainingSessions = request.NumberOfSessions,
            StartDate = DateTime.UtcNow,
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedByAdminId = adminId,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes
        };

        _dbContext.UserTrainingBalances.Add(balance);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created single sessions balance {BalanceId} with {NumberOfSessions} sessions for user {UserId} by admin {AdminId}.",
            balance.Id,
            request.NumberOfSessions,
            userId,
            adminId);

        return balance.ToResponse();
    }

    private static bool IsMonthlyPackage(PurchaseType purchaseType)
    {
        return purchaseType is PurchaseType.Package6 or PurchaseType.Package12 or PurchaseType.Package16;
    }

    private static DateTime? FindPaymentDate(
        UserTrainingBalance balance,
        IEnumerable<MembershipPaymentLookup> paymentDates)
    {
        var payment = paymentDates.FirstOrDefault(payment =>
            payment.PaymentType == balance.PurchaseType
            && payment.NumberOfSessions == GetBasePackageSessionCount(balance.PurchaseType)
            && payment.StartDate == balance.StartDate);

        return payment?.PaymentDate;
    }

    private async Task<DateTime> ResolveNewMonthlyPackageStartDateAsync(
        Guid userId,
        DateTime requestedStartDate,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var latestScheduledMembershipEndDate = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.UserId == userId
                && (balance.PurchaseType == PurchaseType.Package6
                    || balance.PurchaseType == PurchaseType.Package12
                    || balance.PurchaseType == PurchaseType.Package16)
                && balance.IsActive
                && !balance.IsExpired
                && balance.RemainingSessions > 0
                && balance.EndDate.HasValue
                && (balance.StartDate > utcNow
                    || (balance.StartDate <= utcNow && balance.EndDate >= utcNow)))
            .Where(balance => _dbContext.Payments.Any(payment =>
                payment.UserId == balance.UserId
                && payment.PaymentType == balance.PurchaseType
                && payment.StartDate == balance.StartDate))
            .OrderByDescending(balance => balance.EndDate)
            .ThenByDescending(balance => balance.StartDate)
            .Select(balance => balance.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestScheduledMembershipEndDate.HasValue
            && requestedStartDate < latestScheduledMembershipEndDate.Value)
        {
            return latestScheduledMembershipEndDate.Value;
        }

        return requestedStartDate;
    }

    private static int GetBasePackageSessionCount(PurchaseType purchaseType)
    {
        return purchaseType switch
        {
            PurchaseType.Package6 => 6,
            PurchaseType.Package12 => 12,
            PurchaseType.Package16 => 16,
            _ => 0
        };
    }

    private sealed record MembershipPaymentLookup(
        PurchaseType PaymentType,
        DateTime? StartDate,
        DateTime PaymentDate,
        int NumberOfSessions);
}
