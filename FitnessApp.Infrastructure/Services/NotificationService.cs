using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Application.Features.Notifications.Mappings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<NotificationResponse> CreateNotificationAsync(
        CreateNotificationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminId(adminId);
        ValidateRequest(request);

        var notification = CreateNotification(request, adminId);

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created notification {NotificationId} by admin {AdminId}.",
            notification.Id,
            adminId);

        return notification.ToResponse();
    }

    public async Task<NotificationResponse> SendGlobalNotificationAsync(
        CreateNotificationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminId(adminId);
        ValidateRequest(request);

        var verifiedUsers = await _dbContext.Users
            .Where(user => !user.IsDeleted && user.UserStatus == UserStatus.Verified)
            .ToListAsync(cancellationToken);

        var notification = CreateNotification(request, adminId);

        foreach (var user in verifiedUsers)
        {
            notification.UserNotifications.Add(new UserNotification
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (notification.SendEmail)
        {
            EnqueueNotificationEmails(notification, verifiedUsers);
        }

        _logger.LogInformation(
            "Sent global notification {NotificationId} to {UserCount} verified users by admin {AdminId}. Email requested: {SendEmail}.",
            notification.Id,
            verifiedUsers.Count,
            adminId,
            notification.SendEmail);

        return notification.ToResponse();
    }

    public async Task<PaginatedResponse<NotificationResponse>> GetMyNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly = false,
        NotificationType? type = null,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var query = _dbContext.UserNotifications
            .AsNoTracking()
            .Include(userNotification => userNotification.Notification)
            .Where(userNotification => userNotification.UserId == userId)
            .AsQueryable();

        if (unreadOnly)
        {
            query = query.Where(userNotification => !userNotification.IsRead);
        }

        if (type.HasValue)
        {
            query = query.Where(userNotification => userNotification.Notification.Type == type.Value);
        }

        query = query.OrderByDescending(userNotification => userNotification.CreatedAt);

        return await GetPaginatedUserNotificationsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid userId,
        Guid userNotificationId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        if (userNotificationId == Guid.Empty)
        {
            throw new BadRequestException("Notifikacija je obavezna.");
        }

        var userNotification = await _dbContext.UserNotifications
            .FirstOrDefaultAsync(
                notification => notification.Id == userNotificationId && notification.UserId == userId,
                cancellationToken);

        if (userNotification is null)
        {
            throw new NotFoundException("Notifikacija nije pronađena.");
        }

        if (!userNotification.IsRead)
        {
            userNotification.IsRead = true;
            userNotification.ReadAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Marked user notification {UserNotificationId} as read for user {UserId}.",
                userNotification.Id,
                userId);
        }
    }

    public async Task<PaginatedResponse<NotificationResponse>> GetNotificationsAsync(
        int page,
        int pageSize,
        NotificationType? type = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(notification => notification.Type == type.Value);
        }

        query = query.OrderByDescending(notification => notification.CreatedAt);

        return await GetPaginatedNotificationsAsync(query, page, pageSize, cancellationToken);
    }

    private void EnqueueNotificationEmails(
        Notification notification,
        IReadOnlyCollection<ApplicationUser> users)
    {
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning(
                    "Notification email skipped for notification {NotificationId} because user {UserId} has no email.",
                    notification.Id,
                    user.Id);
                continue;
            }

            _backgroundJobClient.Enqueue<NotificationEmailJob>(
                job => job.SendAsync(user.Email, user.FirstName, notification.Title, notification.Message));
        }
    }

    private static Notification CreateNotification(
        CreateNotificationRequest request,
        Guid adminId)
    {
        return new Notification
        {
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Type = request.Type,
            SendEmail = request.SendEmail,
            CreatedByAdminId = adminId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task<PaginatedResponse<NotificationResponse>> GetPaginatedUserNotificationsAsync(
        IQueryable<UserNotification> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var notifications = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = notifications
            .Select(notification => notification.ToResponse())
            .ToArray();

        return new PaginatedResponse<NotificationResponse>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    private static async Task<PaginatedResponse<NotificationResponse>> GetPaginatedNotificationsAsync(
        IQueryable<Notification> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var notifications = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = notifications
            .Select(notification => notification.ToResponse())
            .ToArray();

        return new PaginatedResponse<NotificationResponse>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    private static void ValidateRequest(CreateNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BadRequestException("Naslov je obavezan.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("Poruka je obavezna.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new BadRequestException("Tip notifikacije nije validan.");
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new BadRequestException("Korisnik je obavezan.");
        }
    }

    private static void ValidateAdminId(Guid adminId)
    {
        if (adminId == Guid.Empty)
        {
            throw new BadRequestException("Admin je obavezan.");
        }
    }
}
