using FitnessApp.Domain.Enums;

namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a user's reservation for a training session.
/// </summary>
public class Reservation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TrainingSessionId { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    public bool CancelledByUser { get; set; }

    public bool CancelledByAdmin { get; set; }

    public DateTime? AttendedAt { get; set; }

    public DateTime? NoShowAt { get; set; }

    public DateTime? ReminderSentAt { get; set; }

    public bool AutoMarkedAttended { get; set; }

    public DateTime? AutoMarkedAt { get; set; }

    public string? Notes { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public TrainingSession TrainingSession { get; set; } = null!;
}
