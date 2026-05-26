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
        await EnsureUserExistsAsync(userId, cancellationToken);

        var activeBalances = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.UserId == userId
                && balance.IsActive
                && !balance.IsExpired)
            .OrderBy(balance => balance.EndDate)
            .ThenByDescending(balance => balance.CreatedAt)
            .ToListAsync(cancellationToken);

        var activePackage = activeBalances
            .Where(balance => balance.PurchaseType is PurchaseType.Package12 or PurchaseType.Package6)
            .FirstOrDefault();

        var singleSessionsRemaining = activeBalances
            .Where(balance => balance.PurchaseType == PurchaseType.SingleSessions)
            .Sum(balance => balance.RemainingSessions);

        var totalRemainingSessions = activeBalances.Sum(balance => balance.RemainingSessions);

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
        throw new BadRequestException("Dodavanje pojedinačnih termina biće implementirano u narednom koraku.");
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

    private async Task<UserTrainingBalanceResponse> CreateMonthlyPackageAsync(
        Guid userId,
        DateTime startDate,
        string? notes,
        Guid adminId,
        PurchaseType purchaseType,
        int totalSessions,
        CancellationToken cancellationToken)
    {
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

        _logger.LogInformation(
            "Created {PurchaseType} balance {BalanceId} for user {UserId} by admin {AdminId}.",
            purchaseType,
            balance.Id,
            userId,
            adminId);

        return balance.ToResponse();
    }
}
