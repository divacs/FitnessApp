namespace FitnessApp.Application.Features.Memberships.DTOs;

public class CurrentBalanceResponse
{
    public UserTrainingBalanceResponse? ActivePackage { get; init; }

    public int SingleSessionsRemaining { get; init; }

    public int TotalRemainingSessions { get; init; }

    public bool HasAvailableSessions { get; init; }

    public DateTime? MembershipExpiresAt { get; init; }
}
