using FitnessApp.Application.Features.Auth.DTOs;

namespace FitnessApp.Application.Features.Auth.Interfaces;

/// <summary>
/// Defines registration, login, refresh-token rotation, logout, and current-user lookup operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user in an unverified state.
    /// </summary>
    Task<CurrentUserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a verified user and returns a new token pair.
    /// </summary>
    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a valid refresh token and returns a replacement token pair.
    /// </summary>
    Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an active refresh token.
    /// </summary>
    Task RevokeTokenAsync(
        RevokeTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current user profile for the supplied user id.
    /// </summary>
    Task<CurrentUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
