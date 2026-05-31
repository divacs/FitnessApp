using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Endpoint-i za registraciju, prijavu i upravljanje korisničkom sesijom.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registruje novog korisnika i vraća osnovne podatke o nalogu.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _authService.RegisterAsync(request, cancellationToken);

        return Ok(ApiResponse<CurrentUserResponse>.Success(
            user,
            "Registracija je uspešna. Sačekajte verifikaciju naloga."));
    }

    /// <summary>
    /// Prijavljuje korisnika i vraća access i refresh token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResponse>.Success(response));
    }

    /// <summary>
    /// Osvežava JWT sesiju na osnovu validnog refresh token-a.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResponse>.Success(response));
    }

    /// <summary>
    /// Opoziva refresh token i odjavljuje korisnika sa uređaja.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> Logout(
        RevokeTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(request, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Uspešno ste se odjavili."));
    }

    /// <summary>
    /// Vraća podatke o trenutno prijavljenom korisniku.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Me(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var response = await _authService.GetCurrentUserAsync(userId, cancellationToken);

        return Ok(ApiResponse<CurrentUserResponse>.Success(response));
    }
}
