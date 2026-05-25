using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Application.Features.Users.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await _userService.GetProfileAsync(userId, cancellationToken);

        return Ok(ApiResponse<UserProfileResponse>.Success(profile));
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await _userService.UpdateProfileAsync(userId, request, cancellationToken);

        return Ok(ApiResponse<UserProfileResponse>.Success(profile, "Profil je uspešno ažuriran."));
    }

    [HttpPut("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _userService.ChangePasswordAsync(userId, request, cancellationToken);

        return Ok(ApiResponse<object>.Success(new { }, "Lozinka je uspešno promenjena."));
    }
}
