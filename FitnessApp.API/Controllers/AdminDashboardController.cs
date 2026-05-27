using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminDashboardController(IAdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AdminDashboardResponse>>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var dashboard = await _adminDashboardService.GetAdminDashboardAsync(cancellationToken);

        return Ok(ApiResponse<AdminDashboardResponse>.Success(dashboard));
    }
}
