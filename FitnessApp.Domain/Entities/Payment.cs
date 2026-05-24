using FitnessApp.Domain.Enums;

namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a recorded user payment.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public PurchaseType PaymentType { get; set; }

    public int NumberOfSessions { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByAdminId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ApplicationUser? CreatedByAdmin { get; set; }
}
