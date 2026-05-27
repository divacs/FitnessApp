using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.API.Extensions;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/reservations")]
public class AdminReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public AdminReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<ReservationResponse>>>> GetReservations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? date = null,
        [FromQuery] ReservationStatus? status = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? trainingSessionId = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _reservationService.GetReservationsAsync(
            page,
            pageSize,
            date,
            status,
            userId,
            trainingSessionId,
            sortBy,
            sortDescending,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<ReservationResponse>>.Success(reservations));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReservationResponse>>> GetReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<ReservationResponse>.Success(reservation));
    }

    [HttpPost("{id:guid}/attended")]
    public async Task<ActionResult<ApiResponse<ReservationResponse>>> MarkAsAttended(
        Guid id,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var reservation = await _reservationService.MarkAsAttendedAsync(id, adminId, cancellationToken);

        return Ok(ApiResponse<ReservationResponse>.Success(reservation, "Rezervacija je označena kao prisutna."));
    }

    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<ApiResponse<ReservationResponse>>> MarkAsNoShow(
        Guid id,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var reservation = await _reservationService.MarkAsNoShowAsync(id, adminId, cancellationToken);

        return Ok(ApiResponse<ReservationResponse>.Success(reservation, "Rezervacija je označena kao nedolazak."));
    }
}
