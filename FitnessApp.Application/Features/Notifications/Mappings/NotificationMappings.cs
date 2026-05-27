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
        return new NotificationResponse
        {
            Id = userNotification.NotificationId,
            UserNotificationId = userNotification.Id,
            Title = userNotification.Notification.Title,
            Message = userNotification.Notification.Message,
            Type = userNotification.Notification.Type,
            SendEmail = userNotification.Notification.SendEmail,
            IsRead = userNotification.IsRead,
            ReadAt = userNotification.ReadAt,
            CreatedAt = userNotification.CreatedAt,
            CreatedByAdminId = userNotification.Notification.CreatedByAdminId
        };
    }
}
