using FitnessApp.Application.Features.Auth.DTOs;

namespace FitnessApp.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<CurrentUserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(
        RevokeTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
