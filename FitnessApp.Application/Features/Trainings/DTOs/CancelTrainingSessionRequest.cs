namespace FitnessApp.Application.Features.Trainings.DTOs;

public class CancelTrainingSessionRequest
{
    public string CancellationReason { get; init; } = string.Empty;
}
