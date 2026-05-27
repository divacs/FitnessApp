using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.VerifiedUsersOnly)]
[Route("api/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<ReservationResponse>>> CancelReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var reservation = await _reservationService.CancelReservationAsync(id, userId, cancellationToken);

        return Ok(ApiResponse<ReservationResponse>.Success(reservation, "Rezervacija je uspešno otkazana."));
    }
}
