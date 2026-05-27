using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Application.Features.Memberships.Mappings;
using FitnessApp.Application.Features.Payments.Mappings;
using FitnessApp.Application.Features.Reservations.Mappings;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private const int DashboardListLimit = 10;
    private const int ExpiringSoonDays = 3;

    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(
        AppDbContext dbContext,
        ILogger<AdminDashboardService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminDashboardResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var weekStart = GetUtcWeekStart(utcNow);
        var weekEnd = weekStart.AddDays(7);
        var expiringUntil = utcNow.AddDays(ExpiringSoonDays);

        var totalUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => !user.IsDeleted, cancellationToken);

        var verifiedUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => !user.IsDeleted && user.UserStatus == UserStatus.Verified, cancellationToken);

        var unverifiedUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => !user.IsDeleted && user.UserStatus == UserStatus.Unverified, cancellationToken);

        var blockedUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => !user.IsDeleted && user.UserStatus == UserStatus.Blocked, cancellationToken);

        var reservationsThisWeek = await _dbContext.Reservations
            .AsNoTracking()
            .CountAsync(
                reservation =>
                    reservation.TrainingSession.StartTime >= weekStart
                    && reservation.TrainingSession.StartTime < weekEnd,
                cancellationToken);

        var trainingsThisWeek = await _dbContext.TrainingSessions
            .AsNoTracking()
            .CountAsync(
                training => training.StartTime >= weekStart && training.StartTime < weekEnd,
                cancellationToken);

        var pendingVerificationUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted && user.UserStatus == UserStatus.Unverified)
            .OrderBy(user => user.CreatedAt)
            .Take(DashboardListLimit)
            .ToListAsync(cancellationToken);

        var verifiedUsersWithoutSessions = await GetVerifiedUsersWithoutSessionsAsync(utcNow, cancellationToken);

        var packagesExpiringSoon = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.IsActive
                && !balance.IsExpired
                && (balance.PurchaseType == PurchaseType.Package12 || balance.PurchaseType == PurchaseType.Package6)
                && balance.EndDate >= utcNow
                && balance.EndDate <= expiringUntil)
            .OrderBy(balance => balance.EndDate)
            .Take(DashboardListLimit)
            .ToListAsync(cancellationToken);

        var latestPayments = await _dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.User)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(DashboardListLimit)
            .ToListAsync(cancellationToken);

        var latestReservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .OrderByDescending(reservation => reservation.ReservedAt)
            .Take(DashboardListLimit)
            .ToListAsync(cancellationToken);

        var autoMarkedAttendances = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation => reservation.AutoMarkedAttended)
            .OrderByDescending(reservation => reservation.AutoMarkedAt)
            .Take(DashboardListLimit)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Loaded admin dashboard.");

        return new AdminDashboardResponse
        {
            TotalUsers = totalUsers,
            VerifiedUsers = verifiedUsers,
            UnverifiedUsers = unverifiedUsers,
            BlockedUsers = blockedUsers,
            ReservationsThisWeek = reservationsThisWeek,
            TrainingsThisWeek = trainingsThisWeek,
            PendingVerificationUsers = pendingVerificationUsers.Select(ToUserListResponse).ToArray(),
            UsersWithoutSessions = verifiedUsersWithoutSessions.Select(ToUserListResponse).ToArray(),
            PackagesExpiringSoon = packagesExpiringSoon.Select(balance => balance.ToResponse()).ToArray(),
            LatestPayments = latestPayments.Select(payment => payment.ToResponse()).ToArray(),
            LatestReservations = latestReservations.Select(reservation => reservation.ToResponse()).ToArray(),
            AutoMarkedAttendances = autoMarkedAttendances.Select(reservation => reservation.ToResponse()).ToArray()
        };
    }

    private async Task<IReadOnlyCollection<ApplicationUser>> GetVerifiedUsersWithoutSessionsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var verifiedUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted && user.UserStatus == UserStatus.Verified)
            .OrderBy(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        var usersWithAvailableSessions = await _dbContext.UserTrainingBalances
            .AsNoTracking()
            .Where(balance =>
                balance.IsActive
                && !balance.IsExpired
                && balance.RemainingSessions > 0
                && (balance.PurchaseType == PurchaseType.SingleSessions
                    || ((balance.PurchaseType == PurchaseType.Package12 || balance.PurchaseType == PurchaseType.Package6)
                        && balance.EndDate >= utcNow)))
            .Select(balance => balance.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usersWithAvailableSessionsSet = usersWithAvailableSessions.ToHashSet();

        return verifiedUsers
            .Where(user => !usersWithAvailableSessionsSet.Contains(user.Id))
            .Take(DashboardListLimit)
            .ToArray();
    }

    private static DateTime GetUtcWeekStart(DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

        return utcNow.Date.AddDays(-daysSinceMonday);
    }

    private static UserListResponse ToUserListResponse(ApplicationUser user)
    {
        return new UserListResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            UserStatus = user.UserStatus,
            VerifiedAt = user.VerifiedAt,
            BlockedAt = user.BlockedAt,
            UnblockedAt = user.UnblockedAt,
            CreatedAt = user.CreatedAt
        };
    }
}
