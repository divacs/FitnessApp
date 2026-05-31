using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Admin endpoint-i za pregled i upravljanje korisničkim paketima i terminima.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin")]
public class AdminBalancesController : ControllerBase
{
    private readonly IBalanceService _balanceService;

    public AdminBalancesController(IBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// Vraća sva stanja termina za izabranog korisnika.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpGet("users/{userId:guid}/balances")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserTrainingBalanceResponse>>>> GetUserBalances(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var balances = await _balanceService.GetUserBalancesAsync(userId, cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<UserTrainingBalanceResponse>>.Success(balances));
    }

    /// <summary>
    /// Vraća trenutno aktivno stanje termina za izabranog korisnika.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpGet("users/{userId:guid}/balances/current")]
    public async Task<ActionResult<ApiResponse<CurrentBalanceResponse>>> GetCurrentBalance(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var balance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);

        return Ok(ApiResponse<CurrentBalanceResponse>.Success(balance));
    }

    /// <summary>
    /// Dodaje paket od 12 termina korisniku.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="request">Podaci za novi paket.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpPost("users/{userId:guid}/balances/package-12")]
    public async Task<ActionResult<ApiResponse<UserTrainingBalanceResponse>>> CreatePackage12(
        Guid userId,
        CreatePackage12Request request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var balance = await _balanceService.CreatePackage12Async(userId, request, adminId, cancellationToken);

        return Ok(ApiResponse<UserTrainingBalanceResponse>.Success(balance, "Paket od 12 termina je dodat."));
    }

    /// <summary>
    /// Dodaje paket od 6 termina korisniku.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="request">Podaci za novi paket.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpPost("users/{userId:guid}/balances/package-6")]
    public async Task<ActionResult<ApiResponse<UserTrainingBalanceResponse>>> CreatePackage6(
        Guid userId,
        CreatePackage6Request request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var balance = await _balanceService.CreatePackage6Async(userId, request, adminId, cancellationToken);

        return Ok(ApiResponse<UserTrainingBalanceResponse>.Success(balance, "Paket od 6 termina je dodat."));
    }

    /// <summary>
    /// Dodaje pojedinačne termine korisniku bez kreiranja paketa.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="request">Podaci o broju termina za dodavanje.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpPost("users/{userId:guid}/balances/single-sessions")]
    public async Task<ActionResult<ApiResponse<UserTrainingBalanceResponse>>> AddSingleSessions(
        Guid userId,
        AddSingleSessionsRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var balance = await _balanceService.AddSingleSessionsAsync(userId, request, adminId, cancellationToken);

        return Ok(ApiResponse<UserTrainingBalanceResponse>.Success(balance, "Pojedinačni termini su dodati."));
    }

    /// <summary>
    /// Ažurira postojeće stanje termina.
    /// </summary>
    /// <param name="id">Identifikator stanja termina.</param>
    /// <param name="request">Podaci za izmenu stanja termina.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpPut("balances/{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserTrainingBalanceResponse>>> UpdateBalance(
        Guid id,
        UpdateBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var balance = await _balanceService.UpdateBalanceAsync(id, request, cancellationToken);

        return Ok(ApiResponse<UserTrainingBalanceResponse>.Success(balance, "Stanje termina je ažurirano."));
    }

    /// <summary>
    /// Briše stanje termina iz sistema.
    /// </summary>
    /// <param name="id">Identifikator stanja termina.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpDelete("balances/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> DeleteBalance(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _balanceService.DeleteBalanceAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Stanje termina je obrisano."));
    }
}
