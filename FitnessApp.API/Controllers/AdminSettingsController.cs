using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Settings.DTOs;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/settings")]
public class AdminSettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public AdminSettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<SettingsResponse>>> GetSettings(
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        return Ok(ApiResponse<SettingsResponse>.Success(settings));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<SettingsResponse>>> UpdateSettings(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.UpdateSettingsAsync(request, cancellationToken);

        return Ok(ApiResponse<SettingsResponse>.Success(settings, "Podešavanja su uspešno ažurirana."));
    }
}
