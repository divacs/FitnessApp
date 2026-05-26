namespace FitnessApp.Application.Features.Trainings.DTOs;

public class TrainingSessionResponse
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int Capacity { get; init; }

    public int ReservedCount { get; init; }

    public int AvailableSpots { get; init; }

    public string TrainerName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool IsCancelled { get; init; }

    public string? CancellationReason { get; init; }
}
