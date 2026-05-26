using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

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

    [HttpGet("balance")]
    public async Task<ActionResult<ApiResponse<CurrentBalanceResponse>>> GetCurrentBalance(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var balance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);

        return Ok(ApiResponse<CurrentBalanceResponse>.Success(balance));
    }

    [HttpGet("balances/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BalanceHistoryResponse>>>> GetBalanceHistory(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var balances = await _balanceService.GetBalanceHistoryAsync(userId, cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<BalanceHistoryResponse>>.Success(balances));
    }
}
