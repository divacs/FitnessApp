using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Notifications.Mappings;

public static class NotificationMappings
{
    public static NotificationResponse ToResponse(this Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            SendEmail = notification.SendEmail,
            IsRead = false,
            CreatedAt = notification.CreatedAt,
            CreatedByAdminId = notification.CreatedByAdminId
        };
    }

    public static NotificationResponse ToResponse(this UserNotification userNotification)
    {
        var notification = userNotification.Notification;

        return new NotificationResponse
        {
            Id = userNotification.NotificationId,
            UserNotificationId = userNotification.Id,
            Title = notification?.Title ?? string.Empty,
            Message = notification?.Message ?? string.Empty,
            Type = notification?.Type ?? default,
            SendEmail = notification?.SendEmail ?? false,
            IsRead = userNotification.IsRead,
            ReadAt = userNotification.ReadAt,
            CreatedAt = userNotification.CreatedAt,
            CreatedByAdminId = notification?.CreatedByAdminId
        };
    }
}
