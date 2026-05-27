using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Domain.Enums;

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

    Task<PaginatedResponse<ReservationResponse>> GetReservationsAsync(
        int page,
        int pageSize,
        DateTime? date = null,
        ReservationStatus? status = null,
        Guid? userId = null,
        Guid? trainingSessionId = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> GetReservationByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> MarkAsAttendedAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> MarkAsNoShowAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
