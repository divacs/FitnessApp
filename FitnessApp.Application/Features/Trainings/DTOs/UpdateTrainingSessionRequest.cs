namespace FitnessApp.Application.Features.Trainings.DTOs;

public class UpdateTrainingSessionRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int Capacity { get; init; }

    public bool IsCancelled { get; init; }

    public string? CancellationReason { get; init; }
}
