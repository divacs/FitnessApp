using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Auth.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(ApplicationUser user);

    string GenerateRefreshToken();

    DateTime GetAccessTokenExpiration();

    DateTime GetRefreshTokenExpiration();
}
