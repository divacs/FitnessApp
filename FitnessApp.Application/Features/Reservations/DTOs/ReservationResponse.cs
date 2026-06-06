using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Reservations.DTOs;

/// <summary>
/// Represents a reservation with training, status, and attendance timeline data.
/// </summary>
public class ReservationResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string UserFullName { get; init; } = string.Empty;

    public string UserEmail { get; init; } = string.Empty;

    public Guid TrainingSessionId { get; init; }

    public string TrainingTitle { get; init; } = string.Empty;

    public DateTime TrainingStartTime { get; init; }

    public DateTime TrainingEndTime { get; init; }

    public string TrainerName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public ReservationStatus Status { get; init; }

    public DateTime ReservedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public bool CancelledByUser { get; init; }

    public bool CancelledByAdmin { get; init; }

    public DateTime? AttendedAt { get; init; }

    public DateTime? NoShowAt { get; init; }

    public DateTime? ReminderSentAt { get; init; }

    public bool AutoMarkedAttended { get; init; }

    public DateTime? AutoMarkedAt { get; init; }

    public string? Notes { get; init; }
}
