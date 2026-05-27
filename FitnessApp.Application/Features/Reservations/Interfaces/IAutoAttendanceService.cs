namespace FitnessApp.Application.Features.Reservations.Interfaces;

public interface IAutoAttendanceService
{
    Task AutoMarkAttendanceAsync(CancellationToken cancellationToken = default);
}
