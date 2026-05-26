using FitnessApp.Application.Features.Reservations.DTOs;

namespace FitnessApp.Application.Features.Reservations.Interfaces;

public interface IReservationService
{
    Task<ReservationResponse> ReserveAsync(
        Guid userId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> CancelReservationAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReservationResponse>> GetMyReservationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReservationResponse>> GetUpcomingReservationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
