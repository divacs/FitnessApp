using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Register(RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(request);

        return Ok(ApiResponse<CurrentUserResponse>.Success(
            user,
            "Registracija je uspešna. Sačekajte verifikaciju naloga."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        return Ok(ApiResponse<AuthResponse>.Success(response));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(request);

        return Ok(ApiResponse<AuthResponse>.Success(response));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Logout(RevokeTokenRequest request)
    {
        await _authService.RevokeTokenAsync(request);

        return Ok(ApiResponse<object>.Success(new { }, "Uspešno ste se odjavili."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Me()
    {
        var userId = User.GetUserId();
        var response = await _authService.GetCurrentUserAsync(userId);

        return Ok(ApiResponse<CurrentUserResponse>.Success(response));
    }
}
