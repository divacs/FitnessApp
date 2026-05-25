using FitnessApp.Application.Features.Auth.DTOs;

namespace FitnessApp.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request);

    Task<AuthResponse> LoginAsync(LoginRequest request);

    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    Task RevokeTokenAsync(RevokeTokenRequest request);

    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId);
}
