namespace FitnessApp.Application.Features.Reservations.DTOs;

public class RecordManualAttendanceRequest
{
    public Guid UserId { get; init; }

    public Guid TrainingSessionId { get; init; }

    public string? Notes { get; init; }
}
