namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class DashboardUpcomingReservationResponse
{
    public Guid TrainingSessionId { get; init; }

    public string TrainingTitle { get; init; } = string.Empty;

    public DateTime TrainingStartTime { get; init; }

    public DateTime TrainingEndTime { get; init; }

    public string TrainerName { get; init; } = string.Empty;
}
