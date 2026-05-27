using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Notifications.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }

    public Guid? UserNotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public bool SendEmail { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedByAdminId { get; set; }
}
