namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a notification assigned to a user.
/// </summary>
public class UserNotification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid NotificationId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;

    public Notification Notification { get; set; } = null!;
}
