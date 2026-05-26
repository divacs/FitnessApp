namespace FitnessApp.Application.Features.Memberships.DTOs;

public class AddSingleSessionsRequest
{
    public int NumberOfSessions { get; init; }

    public string? Notes { get; init; }
}
