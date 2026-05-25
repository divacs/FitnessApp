using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Auth.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(ApplicationUser user);

    DateTime GetTokenExpiration();
}
