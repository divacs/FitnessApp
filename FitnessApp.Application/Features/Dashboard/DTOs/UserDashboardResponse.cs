using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Reservations.DTOs;

namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class UserDashboardResponse
{
    public DashboardUserInfoResponse User { get; init; } = new();

    public CurrentBalanceResponse CurrentBalance { get; init; } = new();

    public UserTrainingBalanceResponse? ActivePackage { get; init; }

    public DateTime? MembershipExpiresAt { get; init; }

    public int SingleSessionsRemaining { get; init; }

    public IReadOnlyCollection<ReservationResponse> UpcomingReservations { get; init; } = Array.Empty<ReservationResponse>();

    public IReadOnlyCollection<NotificationResponse> LatestNotifications { get; init; } = Array.Empty<NotificationResponse>();

    public bool IsMembershipExpiringSoon { get; init; }

    public string? MembershipExpirationWarning { get; init; }
}
