namespace FitnessApp.Domain.Enums;

/// <summary>
/// Defines the category of a notification sent to users.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// General informational notification.
    /// </summary>
    General = 1,

    /// <summary>
    /// Notification that a training was cancelled.
    /// </summary>
    TrainingCancelled = 2,

    /// <summary>
    /// Notification that training details were updated.
    /// </summary>
    TrainingUpdated = 3,

    /// <summary>
    /// Notification that a membership is close to expiration.
    /// </summary>
    MembershipExpiring = 4,

    /// <summary>
    /// System-level notification.
    /// </summary>
    System = 5
}
