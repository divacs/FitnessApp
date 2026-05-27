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

        var payment = new Payment
        {
            UserId = request.UserId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            PaymentType = request.PaymentType,
            NumberOfSessions = GetNumberOfSessions(request),
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await CreateOrUpdateBalanceAsync(request, adminId, cancellationToken);

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
        payment.PaymentDate = request.PaymentDate;
        payment.Note = request.Note;
        payment.UpdatedAt = DateTime.UtcNow;

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

        _dbContext.Payments.Remove(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted payment {PaymentId}.", paymentId);
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

        if (request.PaymentType is PurchaseType.Package12 or PurchaseType.Package6)
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
    }

    private async Task CreateOrUpdateBalanceAsync(
        CreatePaymentRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        switch (request.PaymentType)
        {
            case PurchaseType.Package12:
                await _balanceService.CreatePackage12Async(
                    request.UserId,
                    new CreatePackage12Request
                    {
                        StartDate = GetRequiredStartDate(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                break;

            case PurchaseType.Package6:
                await _balanceService.CreatePackage6Async(
                    request.UserId,
                    new CreatePackage6Request
                    {
                        StartDate = GetRequiredStartDate(request),
                        Notes = request.Note
                    },
                    adminId,
                    cancellationToken);
                break;

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
                break;

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

    private static int GetNumberOfSessions(CreatePaymentRequest request)
    {
        return request.PaymentType switch
        {
            PurchaseType.Package12 => 12,
            PurchaseType.Package6 => 6,
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

        return request.StartDate.Value;
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
