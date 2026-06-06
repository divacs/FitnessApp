using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Reservations.Interfaces;

/// <summary>
/// Handles reservation creation, cancellation, attendance, no-show, and reservation queries.
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Creates a reservation without requiring an active membership or available balance.
    /// </summary>
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

    /// <summary>
    /// Marks a reserved training as attended and consumes one session.
    /// </summary>
    Task<ReservationResponse> MarkAsAttendedAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a reserved training as no-show and consumes one session.
    /// </summary>
    Task<ReservationResponse> MarkAsNoShowAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
