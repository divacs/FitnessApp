using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Notifications.DTOs;

public class CreateNotificationRequest
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public bool SendEmail { get; set; }
}
