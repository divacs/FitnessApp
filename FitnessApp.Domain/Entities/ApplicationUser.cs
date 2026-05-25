using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents an application user with Identity data and FitnessApp business fields.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// User's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Current verification and access status of the user.
    /// </summary>
    public UserStatus UserStatus { get; set; } = UserStatus.Unverified;

    /// <summary>
    /// Date and time when the user was verified.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Date and time when the user was blocked.
    /// </summary>
    public DateTime? BlockedAt { get; set; }

    /// <summary>
    /// Date and time when the user was unblocked.
    /// </summary>
    public DateTime? UnblockedAt { get; set; }

    /// <summary>
    /// Internal administrator notes about the user.
    /// </summary>
    public string? AdminNotes { get; set; }

    /// <summary>
    /// Indicates whether the user is soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Date and time when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the user was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User's display name built from first and last name.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Reservations made by the user.
    /// </summary>
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    /// <summary>
    /// Payments recorded for the user.
    /// </summary>
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>
    /// Training balances owned by the user.
    /// </summary>
    public ICollection<UserTrainingBalance> TrainingBalances { get; set; } = new List<UserTrainingBalance>();

    /// <summary>
    /// Notifications assigned to the user.
    /// </summary>
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();

    /// <summary>
    /// Refresh tokens issued to the user.
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
