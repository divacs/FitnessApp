using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.VerifiedUsersOnly)]
[Route("api/me/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserDashboardResponse>>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var dashboard = await _dashboardService.GetUserDashboardAsync(userId, cancellationToken);

        return Ok(ApiResponse<UserDashboardResponse>.Success(dashboard));
    }
}
