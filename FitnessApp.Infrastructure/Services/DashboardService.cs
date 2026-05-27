using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Notifications.Mappings;
using FitnessApp.Application.Features.Reservations.Mappings;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingReservationsLimit = 5;
    private const int LatestNotificationsLimit = 5;
    private const int MembershipExpirationWarningDays = 3;

    private readonly AppDbContext _dbContext;
    private readonly IBalanceService _balanceService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        AppDbContext dbContext,
        IBalanceService balanceService,
        ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _balanceService = balanceService;
        _logger = logger;
    }

    public async Task<UserDashboardResponse> GetUserDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BadRequestException("Korisnik je obavezan.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        var currentBalance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var upcomingReservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.UserId == userId
                && reservation.Status == ReservationStatus.Reserved
                && reservation.TrainingSession.StartTime > utcNow)
            .OrderBy(reservation => reservation.TrainingSession.StartTime)
            .Take(UpcomingReservationsLimit)
            .ToListAsync(cancellationToken);

        var latestNotifications = await _dbContext.UserNotifications
            .AsNoTracking()
            .Include(userNotification => userNotification.Notification)
            .Where(userNotification => userNotification.UserId == userId)
            .OrderByDescending(userNotification => userNotification.CreatedAt)
            .Take(LatestNotificationsLimit)
            .ToListAsync(cancellationToken);

        var membershipExpirationWarning = BuildMembershipExpirationWarning(currentBalance.MembershipExpiresAt, utcNow);

        _logger.LogInformation("Loaded dashboard for user {UserId}.", userId);

        return new UserDashboardResponse
        {
            User = new DashboardUserInfoResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                UserStatus = user.UserStatus
            },
            CurrentBalance = currentBalance,
            ActivePackage = currentBalance.ActivePackage,
            MembershipExpiresAt = currentBalance.MembershipExpiresAt,
            SingleSessionsRemaining = currentBalance.SingleSessionsRemaining,
            UpcomingReservations = upcomingReservations
                .Select(reservation => reservation.ToResponse())
                .ToArray(),
            LatestNotifications = latestNotifications
                .Select(notification => notification.ToResponse())
                .ToArray(),
            IsMembershipExpiringSoon = membershipExpirationWarning is not null,
            MembershipExpirationWarning = membershipExpirationWarning
        };
    }

    private static string? BuildMembershipExpirationWarning(
        DateTime? membershipExpiresAt,
        DateTime utcNow)
    {
        if (!membershipExpiresAt.HasValue)
        {
            return null;
        }

        var daysUntilExpiration = (membershipExpiresAt.Value.Date - utcNow.Date).Days;

        if (daysUntilExpiration < 0 || daysUntilExpiration > MembershipExpirationWarningDays)
        {
            return null;
        }

        return daysUntilExpiration == 0
            ? "Članarina ističe danas."
            : $"Članarina ističe za {daysUntilExpiration} dana.";
    }
}
