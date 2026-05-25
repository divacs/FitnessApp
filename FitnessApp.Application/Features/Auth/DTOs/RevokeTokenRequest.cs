namespace FitnessApp.Application.Features.Auth.DTOs;

public class RevokeTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
