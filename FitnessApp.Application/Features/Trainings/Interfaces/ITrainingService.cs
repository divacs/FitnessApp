using FitnessApp.Application.Features.Trainings.DTOs;

namespace FitnessApp.Application.Features.Trainings.Interfaces;

public interface ITrainingService
{
    Task<IReadOnlyCollection<TrainingCalendarResponse>> GetUpcomingTrainingsAsync(
        CancellationToken cancellationToken = default);

    Task<TrainingSessionResponse> GetTrainingByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TrainingSessionResponse> CreateTrainingAsync(
        CreateTrainingSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<TrainingSessionResponse> UpdateTrainingAsync(
        Guid id,
        UpdateTrainingSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<TrainingSessionResponse> CancelTrainingAsync(
        Guid id,
        string? cancellationReason = null,
        CancellationToken cancellationToken = default);

    Task DeleteTrainingAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
