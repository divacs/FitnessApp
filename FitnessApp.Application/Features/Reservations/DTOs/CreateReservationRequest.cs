namespace FitnessApp.Application.Features.Reservations.DTOs;

public class CreateReservationRequest
{
    public Guid TrainingSessionId { get; init; }

    public string? Notes { get; init; }
}
