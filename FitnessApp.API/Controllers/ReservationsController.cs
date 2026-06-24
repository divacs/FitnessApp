using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Endpoint-i koje verifikovani korisnik koristi za upravljanje svojim rezervacijama.
/// </summary>
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

    /// <summary>
    /// Kreira novu rezervaciju za prijavljenog verifikovanog korisnika.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReservationResponse>>> CreateReservation(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var reservation = await _reservationService.ReserveAsync(userId, request, cancellationToken);

        return Ok(ApiResponse<ReservationResponse>.Success(reservation, "Rezervacija je uspešno kreirana."));
    }

    /// <summary>
    /// Otkazuje postojeću korisničku rezervaciju za trening.
    /// </summary>
    /// <param name="id">Identifikator rezervacije.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
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
