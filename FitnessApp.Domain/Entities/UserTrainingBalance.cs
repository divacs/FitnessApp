using FitnessApp.Domain.Enums;

namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a user's available training session balance.
/// </summary>
public class UserTrainingBalance
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public PurchaseType PurchaseType { get; set; }

    public int TotalSessions { get; set; }

    public int RemainingSessions { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsExpired { get; set; }

    public int CarriedOverSessions { get; set; }

    public DateTime? ExpirationReminderSentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByAdminId { get; set; }

    public string? Notes { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ApplicationUser? CreatedByAdmin { get; set; }
}
