namespace FitnessApp.Application.Features.Memberships.DTOs;

public class UpdateBalanceRequest
{
    public int RemainingSessions { get; init; }

    public bool IsActive { get; init; }

    public bool IsExpired { get; init; }

    public string? Notes { get; init; }
}
