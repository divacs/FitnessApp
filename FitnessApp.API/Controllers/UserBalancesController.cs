using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Endpoint-i za pregled članarine i stanja termina prijavljenog korisnika.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.VerifiedUsersOnly)]
[Route("api/me")]
public class UserBalancesController : ControllerBase
{
    private readonly IBalanceService _balanceService;

    public UserBalancesController(IBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// Vraća trenutno aktivno stanje termina za prijavljenog korisnika.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<ApiResponse<CurrentBalanceResponse>>> GetCurrentBalance(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var balance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);

        return Ok(ApiResponse<CurrentBalanceResponse>.Success(balance));
    }

    /// <summary>
    /// Vraća istoriju paketa i promena stanja termina prijavljenog korisnika.
    /// </summary>
    [HttpGet("balances/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BalanceHistoryResponse>>>> GetBalanceHistory(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var balances = await _balanceService.GetBalanceHistoryAsync(userId, cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<BalanceHistoryResponse>>.Success(balances));
    }
}
