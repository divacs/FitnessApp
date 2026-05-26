namespace FitnessApp.Application.Features.Trainings.DTOs;

public class CreateTrainingSessionRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int Capacity { get; init; }

    public string? TrainerName { get; init; }

    public string? Location { get; init; }
}
