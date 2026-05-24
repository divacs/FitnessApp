namespace FitnessApp.Domain.Enums;

/// <summary>
/// Defines the lifecycle state of a training reservation.
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// Reservation is active for an upcoming training.
    /// </summary>
    Reserved = 1,

    /// <summary>
    /// Reservation was cancelled before attendance was recorded.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// User attended the reserved training.
    /// </summary>
    Attended = 3,

    /// <summary>
    /// User did not attend the reserved training.
    /// </summary>
    NoShow = 4
}
