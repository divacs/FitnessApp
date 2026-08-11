using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Pagination;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Application.Features.Payments.Interfaces;
using FitnessApp.Application.Features.Payments.Mappings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _dbContext;
    private readonly IBalanceService _balanceService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext dbContext,
        IBalanceService balanceService,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _balanceService = balanceService;
        _logger = logger;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ValidateCreatePaymentRequest(request);

        await EnsureUserExistsAsync(request.UserId, cancellationToken);

        var paymentStartDate = request.StartDate.HasValue
            ? NormalizeUtc(request.StartDate.Value)
            : (DateTime?)null;

        var payment = new Payment
        {
            UserId = request.UserId,
            Amount = request.Amount,
            PaymentDate = NormalizeUtc(request.PaymentDate),
            StartDate = paymentStartDate,
            PaymentType = request.PaymentType,
            NumberOfSessions = GetNumberOfSessions(request),
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var createdBalance = await CreateOrUpdateBalanceAsync(request, adminId, cancellationToken);

        if (createdBalance is not null && IsPackagePaymentType(request.PaymentType))
        {
            payment.StartDate = createdBalance.StartDate;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await SettleOverdueReservationsAsync(
            payment.UserId,
            payment.PaymentType,
            createdBalance?.Id,
            payment.PaymentDate,
            createdBalance?.TotalSessions ?? GetRequiredNumberOfSessions(request),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Created payment {PaymentId} for user {UserId} with payment type {PaymentType} by admin {AdminId}.",
            payment.Id,
            request.UserId,
            request.PaymentType,
            adminId);

        return await GetPaymentResponseAsync(payment.Id, cancellationToken);
    }

    public async Task<PaymentResponse> UpdatePaymentAsync(
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdatePaymentRequest(request);

        var payment = await _dbContext.Payments
            .Include(payment => payment.User)
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Uplata nije pronađena.");
        }

        payment.Amount = request.Amount;
        payment.PaymentDate = NormalizeUtc(request.PaymentDate);
        payment.StartDate = ResolveUpdatedPaymentStartDate(payment.PaymentType, request.StartDate, payment.StartDate);
        payment.Note = request.Note;
        payment.UpdatedAt = DateTime.UtcNow;

        await UpdateRelatedBalanceAsync(payment, cancellationToken);
        await SettleOverdueReservationsOnExistingBalanceAsync(payment, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated payment {PaymentId}.", paymentId);

        return payment.ToResponse();
    }

    public async Task DeletePaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Uplata nije pronađena.");
        }

        var relatedBalance = await FindRelatedBalanceAsync(payment, cancellationToken);

        if (relatedBalance is not null && payment.PaymentType == PurchaseType.SingleSessions)
        {
            var hasOtherSingleSessionPayments = await _dbContext.Payments
                .AnyAsync(existingPayment =>
                    existingPayment.Id != payment.Id
                    && existingPayment.UserId == payment.UserId
                    && existingPayment.PaymentType == PurchaseType.SingleSessions,
                    cancellationToken);

            if (hasOtherSingleSessionPayments)
            {
                relatedBalance.TotalSessions = Math.Max(0, relatedBalance.TotalSessions - payment.NumberOfSessions);
                relatedBalance.RemainingSessions = Math.Max(0, relatedBalance.RemainingSessions - payment.NumberOfSessions);
                relatedBalance.IsActive = relatedBalance.RemainingSessions > 0;
                relatedBalance.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _dbContext.UserTrainingBalances.Remove(relatedBalance);
            }
        }
        else if (relatedBalance is not null)
        {
            _dbContext.UserTrainingBalances.Remove(relatedBalance);
        }

        _dbContext.Payments.Remove(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted payment {PaymentId} and related balance {BalanceId}.",
            paymentId,
            relatedBalance?.Id);
    }

    public async Task<PaginatedResponse<PaymentResponse>> GetPaymentsAsync(
        int page,
        int pageSize,
        PurchaseType? paymentType = null,
        Guid? userId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.User)
            .AsQueryable();

        query = ApplyFilters(query, paymentType, userId, fromDate, toDate, search);

        return await GetPaginatedPaymentsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PaginatedResponse<PaymentResponse>> GetUserPaymentsAsync(
        Guid userId,
        int page,
        int pageSize,
        PurchaseType? paymentType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var query = _dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.User)
            .Where(payment => payment.UserId == userId)
            .AsQueryable();

        query = ApplyFilters(query, paymentType, userId: null, fromDate, toDate, search);

        return await GetPaginatedPaymentsAsync(query, page, pageSize, cancellationToken);
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
            _logger.LogWarning("Payment requested for missing user {UserId}.", userId);
            throw new NotFoundException("Korisnik nije pronađen.");
        }
    }

    private static void ValidateCreatePaymentRequest(CreatePaymentRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new BadRequestException("Korisnik je obavezan.");
        }

        if (request.Amount < 0)
        {
            throw new BadRequestException("Iznos ne može biti negativan.");
        }

        if (request.PaymentDate == default)
        {
            throw new BadRequestException("Datum uplate je obavezan.");
        }

        if (!Enum.IsDefined(request.PaymentType))
        {
            throw new BadRequestException("Tip uplate nije validan.");
        }

        if (request.PaymentType == PurchaseType.SingleSessions)
        {
            _ = GetRequiredNumberOfSessions(request);
        }

        if (request.PaymentType is PurchaseType.Package12 or PurchaseType.Package6 or PurchaseType.Package16)
        {
            _ = GetRequiredStartDate(request);
        }
    }

    private static void ValidateUpdatePaymentRequest(UpdatePaymentRequest request)
    {
        if (request.Amount < 0)
        {
            throw new BadRequestException("Iznos ne može biti negativan.");
        }

        if (request.PaymentDate == default)
        {
            throw new BadRequestException("Datum uplate je obavezan.");
        }

        if (request.StartDate.HasValue && request.StartDate.Value == default)
        {
            throw new BadRequestException("Datum početka je obavezan za paket.");
        }
    }

    private async Task<UserTrainingBalanceResponse?> CreateOrUpdateBalanceAsync(
        CreatePaymentRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        switch (request.PaymentType)
        {
            case PurchaseType.Package12:
                return await _balanceService.CreatePackage12Async(
                    request.UserId,
                    new CreatePackage12Request
                    {
                        StartDate = GetRequiredStartDate(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                

            case PurchaseType.Package6:
                return await _balanceService.CreatePackage6Async(
                    request.UserId,
                    new CreatePackage6Request
                    {
                        StartDate = GetRequiredStartDate(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                

            case PurchaseType.Package16:
                return await _balanceService.CreatePackage16Async(
                    request.UserId,
                    new CreatePackage16Request
                    {
                        StartDate = GetRequiredStartDate(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                

            case PurchaseType.SingleSessions:
                await _balanceService.AddSingleSessionsAsync(
                    request.UserId,
                    new AddSingleSessionsRequest
                    {
                        NumberOfSessions = GetRequiredNumberOfSessions(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                return null;

            default:
                throw new BadRequestException("Tip uplate nije validan.");
        }
    }

    private async Task<PaymentResponse> GetPaymentResponseAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.User)
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Uplata nije pronađena.");
        }

        return payment.ToResponse();
    }

    private async Task SettleOverdueReservationsAsync(
        Guid userId,
        PurchaseType paymentType,
        Guid? balanceId,
        DateTime paymentDate,
        int availableSessions,
        CancellationToken cancellationToken)
    {
        if (availableSessions <= 0)
        {
            return;
        }

        if (paymentType is not (PurchaseType.Package6 or PurchaseType.Package12 or PurchaseType.Package16 or PurchaseType.SingleSessions))
        {
            return;
        }

        UserTrainingBalance? balance = null;

        if (IsPackagePaymentType(paymentType))
        {
            balance = await _dbContext.UserTrainingBalances
                .FirstOrDefaultAsync(
                    x => x.UserId == userId
                         && (x.PurchaseType == PurchaseType.Package6
                             || x.PurchaseType == PurchaseType.Package12
                             || x.PurchaseType == PurchaseType.Package16)
                         && x.IsActive
                         && !x.IsExpired
                         && x.RemainingSessions > 0
                         && x.StartDate <= DateTime.UtcNow
                         && x.EndDate.HasValue
                         && x.EndDate.Value >= DateTime.UtcNow,
                    cancellationToken);
        }

        if (balance is null && balanceId.HasValue)
        {
            balance = await _dbContext.UserTrainingBalances
                .FirstOrDefaultAsync(x => x.Id == balanceId.Value && x.UserId == userId, cancellationToken);
        }
        else if (balance is null && paymentType == PurchaseType.SingleSessions)
        {
            balance = await _dbContext.UserTrainingBalances
                .FirstOrDefaultAsync(
                    x => x.UserId == userId
                         && x.PurchaseType == PurchaseType.SingleSessions
                         && x.IsActive
                         && !x.IsExpired,
                    cancellationToken);
        }

        if (balance is null || balance.RemainingSessions <= 0)
        {
            return;
        }

        var overdueReservations = await _dbContext.Reservations
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.UserId == userId
                && reservation.Status == ReservationStatus.Reserved
                && reservation.TrainingSession.EndTime <= paymentDate)
            .OrderBy(reservation => reservation.TrainingSession.StartTime)
            .Take(Math.Min(balance.RemainingSessions, availableSessions))
            .ToListAsync(cancellationToken);

        foreach (var reservation in overdueReservations)
        {
            reservation.Status = ReservationStatus.Attended;
            reservation.AttendedAt = paymentDate;
            reservation.AutoMarkedAttended = false;
            reservation.AutoMarkedAt = null;

            balance.RemainingSessions -= 1;
        }

        if (overdueReservations.Count > 0)
        {
            balance.UpdatedAt = DateTime.UtcNow;

            if (IsPackagePaymentType(balance.PurchaseType) && balance.RemainingSessions <= 0)
            {
                balance.IsActive = false;
            }

            _logger.LogInformation(
                "Settled {SettledCount} overdue reservations for user {UserId} after payment {PaymentType}.",
                overdueReservations.Count,
                userId,
                paymentType);
        }
    }

    private async Task SettleOverdueReservationsOnExistingBalanceAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (!IsPackagePaymentType(payment.PaymentType) && payment.PaymentType != PurchaseType.SingleSessions)
        {
            return;
        }

        var balance = await FindRelatedBalanceAsync(payment, cancellationToken);

        if (balance is null)
        {
            return;
        }

        await SettleOverdueReservationsAsync(
            payment.UserId,
            payment.PaymentType,
            balance.Id,
            payment.PaymentDate,
            balance.RemainingSessions,
            cancellationToken);
    }

    private static int GetNumberOfSessions(CreatePaymentRequest request)
    {
        return request.PaymentType switch
        {
            PurchaseType.Package12 => 12,
            PurchaseType.Package6 => 6,
            PurchaseType.Package16 => 16,
            PurchaseType.SingleSessions => GetRequiredNumberOfSessions(request),
            _ => throw new BadRequestException("Tip uplate nije validan.")
        };
    }

    private static int GetRequiredNumberOfSessions(CreatePaymentRequest request)
    {
        if (request.NumberOfSessions is not > 0)
        {
            throw new BadRequestException("Broj termina mora biti veći od 0.");
        }

        return request.NumberOfSessions.Value;
    }

    private static DateTime GetRequiredStartDate(CreatePaymentRequest request)
    {
        if (request.StartDate is null)
        {
            throw new BadRequestException("Datum početka je obavezan za paket.");
        }

        return NormalizeUtc(request.StartDate.Value);
    }

    private async Task UpdateRelatedBalanceAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (!IsPackagePaymentType(payment.PaymentType) || !payment.StartDate.HasValue)
        {
            return;
        }

        var balance = await FindRelatedBalanceAsync(payment, cancellationToken);

        if (balance is null)
        {
            _logger.LogWarning(
                "No related balance found while updating payment {PaymentId} for user {UserId}.",
                payment.Id,
                payment.UserId);
            return;
        }

        var resolvedStartDate = await ResolveUpdatedMembershipStartDateAsync(
            balance,
            payment.StartDate.Value,
            cancellationToken);

        payment.StartDate = resolvedStartDate;
        balance.StartDate = resolvedStartDate;
        balance.EndDate = resolvedStartDate.AddMonths(1);
        balance.UpdatedAt = DateTime.UtcNow;

        await ShiftOverlappingFutureMembershipsAsync(balance, cancellationToken);
    }

    private async Task<UserTrainingBalance?> FindRelatedBalanceAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserTrainingBalances
            .Where(balance =>
                balance.UserId == payment.UserId
                && balance.PurchaseType == payment.PaymentType
                && (IsPackagePaymentType(payment.PaymentType)
                    || payment.PaymentType == PurchaseType.SingleSessions));

        if (payment.CreatedByAdminId.HasValue)
        {
            query = query.Where(balance => balance.CreatedByAdminId == payment.CreatedByAdminId);
        }

        if (payment.StartDate.HasValue)
        {
            var startDate = payment.StartDate.Value;

            return await query
                .OrderByDescending(balance => balance.StartDate == startDate)
                .ThenByDescending(balance => balance.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await query
            .OrderByDescending(balance => balance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateTime> ResolveUpdatedMembershipStartDateAsync(
        UserTrainingBalance balance,
        DateTime requestedStartDate,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var isCurrentlyActiveMembership = balance.RemainingSessions > 0
            && balance.EndDate.HasValue
            && balance.StartDate <= utcNow
            && balance.EndDate.Value >= utcNow;

        if (isCurrentlyActiveMembership)
        {
            return requestedStartDate;
        }

        var latestBlockingEndDate = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(otherBalance =>
                otherBalance.UserId == balance.UserId
                && otherBalance.Id != balance.Id
                && (otherBalance.PurchaseType == PurchaseType.Package6
                    || otherBalance.PurchaseType == PurchaseType.Package12
                    || otherBalance.PurchaseType == PurchaseType.Package16)
                && otherBalance.IsActive
                && !otherBalance.IsExpired
                && otherBalance.RemainingSessions > 0
                && otherBalance.EndDate.HasValue
                && ((otherBalance.StartDate <= utcNow && otherBalance.EndDate.Value >= utcNow)
                    || otherBalance.StartDate <= requestedStartDate))
            .OrderByDescending(otherBalance => otherBalance.EndDate)
            .Select(otherBalance => otherBalance.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestBlockingEndDate.HasValue && requestedStartDate < latestBlockingEndDate.Value)
        {
            return latestBlockingEndDate.Value;
        }

        return requestedStartDate;
    }

    private async Task ShiftOverlappingFutureMembershipsAsync(
        UserTrainingBalance sourceBalance,
        CancellationToken cancellationToken)
    {
        if (!sourceBalance.EndDate.HasValue)
        {
            return;
        }

        var nextAllowedStartDate = sourceBalance.EndDate.Value;
        var futureBalances = await _dbContext.UserTrainingBalances
            .Where(balance =>
                balance.UserId == sourceBalance.UserId
                && balance.Id != sourceBalance.Id
                && (balance.PurchaseType == PurchaseType.Package6
                    || balance.PurchaseType == PurchaseType.Package12
                    || balance.PurchaseType == PurchaseType.Package16)
                && balance.IsActive
                && !balance.IsExpired
                && balance.RemainingSessions > 0
                && balance.EndDate.HasValue
                && balance.StartDate >= sourceBalance.StartDate)
            .OrderBy(balance => balance.StartDate)
            .ThenBy(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var futureBalance in futureBalances)
        {
            if (futureBalance.StartDate < nextAllowedStartDate)
            {
                var previousStartDate = futureBalance.StartDate;

                futureBalance.StartDate = nextAllowedStartDate;
                futureBalance.EndDate = nextAllowedStartDate.AddMonths(1);
                futureBalance.UpdatedAt = DateTime.UtcNow;

                var relatedPayment = await FindPaymentForBalanceStartDateAsync(
                    futureBalance,
                    previousStartDate,
                    cancellationToken);

                if (relatedPayment is not null)
                {
                    relatedPayment.StartDate = futureBalance.StartDate;
                    relatedPayment.UpdatedAt = DateTime.UtcNow;
                }
            }

            nextAllowedStartDate = futureBalance.EndDate!.Value;
        }
    }

    private async Task<Payment?> FindPaymentForBalanceStartDateAsync(
        UserTrainingBalance balance,
        DateTime previousStartDate,
        CancellationToken cancellationToken)
    {
        var baseNumberOfSessions = balance.PurchaseType switch
        {
            PurchaseType.Package6 => 6,
            PurchaseType.Package12 => 12,
            PurchaseType.Package16 => 16,
            _ => balance.TotalSessions
        };

        return await _dbContext.Payments
            .Where(payment =>
                payment.UserId == balance.UserId
                && payment.PaymentType == balance.PurchaseType
                && payment.NumberOfSessions == baseNumberOfSessions)
            .OrderByDescending(payment => payment.StartDate == previousStartDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DateTime? ResolveUpdatedPaymentStartDate(
        PurchaseType paymentType,
        DateTime? requestedStartDate,
        DateTime? currentStartDate)
    {
        if (!IsPackagePaymentType(paymentType))
        {
            return null;
        }

        return requestedStartDate.HasValue
            ? NormalizeUtc(requestedStartDate.Value)
            : currentStartDate;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static bool IsPackagePaymentType(PurchaseType paymentType)
    {
        return paymentType is PurchaseType.Package6 or PurchaseType.Package12 or PurchaseType.Package16;
    }

    private static IQueryable<Payment> ApplyFilters(
        IQueryable<Payment> query,
        PurchaseType? paymentType,
        Guid? userId,
        DateTime? fromDate,
        DateTime? toDate,
        string? search)
    {
        if (paymentType.HasValue)
        {
            query = query.Where(payment => payment.PaymentType == paymentType.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(payment => payment.UserId == userId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(payment => payment.PaymentDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(payment => payment.PaymentDate <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();

            query = query.Where(payment =>
                payment.User.FirstName.Contains(trimmedSearch)
                || payment.User.LastName.Contains(trimmedSearch)
                || (payment.User.Email != null && payment.User.Email.Contains(trimmedSearch))
                || (payment.Note != null && payment.Note.Contains(trimmedSearch)));
        }

        return query;
    }

    private static async Task<PaginatedResponse<PaymentResponse>> GetPaginatedPaymentsAsync(
        IQueryable<Payment> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var payments = await query
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .ApplyPagination(page, pageSize)
            .ToListAsync(cancellationToken);

        var items = payments
            .Select(payment => payment.ToResponse())
            .ToArray();

        return items.ToPaginatedResponse(page, pageSize, totalCount);
    }
}
