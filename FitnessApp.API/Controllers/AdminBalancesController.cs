using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

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

    [HttpGet("users/{userId:guid}/balances")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserTrainingBalanceResponse>>>> GetUserBalances(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var balances = await _balanceService.GetUserBalancesAsync(userId, cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<UserTrainingBalanceResponse>>.Success(balances));
    }

    [HttpGet("users/{userId:guid}/balances/current")]
    public async Task<ActionResult<ApiResponse<CurrentBalanceResponse>>> GetCurrentBalance(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var balance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);

        return Ok(ApiResponse<CurrentBalanceResponse>.Success(balance));
    }

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

    [HttpPut("balances/{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserTrainingBalanceResponse>>> UpdateBalance(
        Guid id,
        UpdateBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var balance = await _balanceService.UpdateBalanceAsync(id, request, cancellationToken);

        return Ok(ApiResponse<UserTrainingBalanceResponse>.Success(balance, "Stanje termina je ažurirano."));
    }

    [HttpDelete("balances/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBalance(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _balanceService.DeleteBalanceAsync(id, cancellationToken);

        return Ok(ApiResponse<object>.Success(new { }, "Stanje termina je obrisano."));
    }
}
