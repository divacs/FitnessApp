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

        var singleSessionsRemaining = await GetAvailableSingleSessionsQuery(userId)
            .AsNoTracking()
            .SumAsync(balance => balance.RemainingSessions, cancellationToken);

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
            .Where(balance => balance.UserId == userId)
            .OrderByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        return balances
            .Select(balance => balance.ToHistoryResponse())
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
            .Where(balance => balance.UserId == userId)
            .OrderByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        return balances
            .Select(balance => balance.ToResponse())
            .ToArray();
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

        var carriedOverSessions = Math.Min(previousPackage.RemainingSessions, MaxCarriedOverSessions);

        newPackage.TotalSessions += carriedOverSessions;
        newPackage.RemainingSessions += carriedOverSessions;
        newPackage.CarriedOverSessions = carriedOverSessions;

        previousPackage.IsExpired = true;
        previousPackage.IsActive = false;

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

        balance ??= await GetAvailableSingleSessionsQuery(userId)
            .OrderBy(balance => balance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (balance is null)
        {
            _logger.LogWarning("Unable to consume session for user {UserId} because no sessions are available.", userId);
            throw new ConflictException("Korisnik nema dostupnih termina.");
        }

        balance.RemainingSessions -= 1;

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
                && (balance.PurchaseType == PurchaseType.Package12 || balance.PurchaseType == PurchaseType.Package6)
                && balance.EndDate >= utcNow);
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

        var balance = new UserTrainingBalance
        {
            UserId = userId,
            PurchaseType = purchaseType,
            TotalSessions = totalSessions,
            RemainingSessions = totalSessions,
            StartDate = startDate,
            EndDate = startDate.AddMonths(1),
            IsActive = true,
            IsExpired = false,
            CreatedByAdminId = adminId,
            CreatedAt = DateTime.UtcNow,
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
}
