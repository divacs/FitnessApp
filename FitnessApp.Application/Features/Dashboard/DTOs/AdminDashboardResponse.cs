using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Users.DTOs;

namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class AdminDashboardResponse
{
    public int TotalUsers { get; init; }

    public int VerifiedUsers { get; init; }

    public int UnverifiedUsers { get; init; }

    public int BlockedUsers { get; init; }

    public int ReservationsThisWeek { get; init; }

    public int TrainingsThisWeek { get; init; }

    public IReadOnlyCollection<UserListResponse> PendingVerificationUsers { get; init; } = Array.Empty<UserListResponse>();

    public IReadOnlyCollection<UserListResponse> UsersWithoutSessions { get; init; } = Array.Empty<UserListResponse>();

    public IReadOnlyCollection<UserTrainingBalanceResponse> PackagesExpiringSoon { get; init; } = Array.Empty<UserTrainingBalanceResponse>();

    public IReadOnlyCollection<PaymentResponse> LatestPayments { get; init; } = Array.Empty<PaymentResponse>();

    public IReadOnlyCollection<ReservationResponse> LatestReservations { get; init; } = Array.Empty<ReservationResponse>();

    public IReadOnlyCollection<ReservationResponse> AutoMarkedAttendances { get; init; } = Array.Empty<ReservationResponse>();
}
