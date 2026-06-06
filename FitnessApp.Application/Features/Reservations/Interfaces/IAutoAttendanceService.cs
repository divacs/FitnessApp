namespace FitnessApp.Application.Features.Reservations.Interfaces;

/// <summary>
/// Automatically marks eligible reserved trainings as attended after the configured delay.
/// </summary>
public interface IAutoAttendanceService
{
    /// <summary>
    /// Processes finished reserved trainings for auto-attendance without ever creating no-show records.
    /// </summary>
    Task AutoMarkAttendanceAsync(CancellationToken cancellationToken = default);
}
