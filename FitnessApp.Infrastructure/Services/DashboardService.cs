using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Notifications.Mappings;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingReservationsLimit = 3;
    private const int LatestNotificationsLimit = 3;

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

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (!userExists)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        var currentBalance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);
        var activeMembership = currentBalance.ActivePackage;
        var utcNow = DateTime.UtcNow;

        var upcomingReservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.UserId == userId
                && reservation.Status == ReservationStatus.Reserved
                && reservation.TrainingSession.StartTime > utcNow)
            .OrderBy(reservation => reservation.TrainingSession.StartTime)
            .Take(UpcomingReservationsLimit)
            .Select(reservation => new DashboardUpcomingReservationResponse
            {
                TrainingSessionId = reservation.TrainingSessionId,
                TrainingTitle = reservation.TrainingSession.Title,
                TrainingStartTime = reservation.TrainingSession.StartTime,
                TrainingEndTime = reservation.TrainingSession.EndTime,
                TrainerName = reservation.TrainingSession.TrainerName
            })
            .ToListAsync(cancellationToken);

        var latestNotifications = await _dbContext.UserNotifications
            .AsNoTracking()
            .Include(userNotification => userNotification.Notification)
            .Where(userNotification => userNotification.UserId == userId)
            .OrderByDescending(userNotification => userNotification.CreatedAt)
            .Take(LatestNotificationsLimit)
            .ToListAsync(cancellationToken);

        var unreadNotificationsCount = await _dbContext.UserNotifications
            .AsNoTracking()
            .CountAsync(
                userNotification => userNotification.UserId == userId && !userNotification.IsRead,
                cancellationToken);

        var activeMembershipPaymentId = await GetActiveMembershipPaymentIdAsync(
            userId,
            activeMembership,
            cancellationToken);

        _logger.LogInformation("Loaded dashboard for user {UserId}.", userId);

        return new UserDashboardResponse
        {
            ActiveMembership = activeMembership is null
                ? null
                : new DashboardActiveMembershipResponse
                {
                    PaymentId = activeMembershipPaymentId,
                    PaymentType = activeMembership.PurchaseType,
                    NumberOfSessions = activeMembership.TotalSessions,
                    RemainingSessions = activeMembership.RemainingSessions,
                    StartDate = activeMembership.StartDate,
                    EndDate = activeMembership.EndDate,
                    Status = "Active"
                },
            UpcomingReservations = upcomingReservations,
            LatestNotifications = latestNotifications
                .Select(notification => notification.ToResponse())
                .ToArray(),
            UnreadNotificationsCount = unreadNotificationsCount
        };
    }

    private async Task<Guid?> GetActiveMembershipPaymentIdAsync(
        Guid userId,
        UserTrainingBalanceResponse? activeMembership,
        CancellationToken cancellationToken)
    {
        if (activeMembership is null)
        {
            return null;
        }

        var payment = await _dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.UserId == userId
                && payment.PaymentType == activeMembership.PurchaseType
                && (
                    activeMembership.PurchaseType == PurchaseType.SingleSessions
                        ? payment.NumberOfSessions == activeMembership.TotalSessions
                        : payment.NumberOfSessions == GetBasePackageSessionCount(activeMembership.PurchaseType)
                          && payment.StartDate == activeMembership.StartDate))
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return payment?.Id;
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
}
