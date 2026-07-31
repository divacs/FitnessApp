using FitnessApp.Application.Features.Notifications.DTOs;

namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class UserDashboardResponse
{
    public DashboardActiveMembershipResponse? ActiveMembership { get; init; }

    public IReadOnlyCollection<DashboardUpcomingReservationResponse> UpcomingReservations { get; init; } = Array.Empty<DashboardUpcomingReservationResponse>();

    public IReadOnlyCollection<NotificationResponse> LatestNotifications { get; init; } = Array.Empty<NotificationResponse>();

    public int UnreadNotificationsCount { get; init; }
}
