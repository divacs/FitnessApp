using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Auth.DTOs;

public class CurrentUserResponse
{
    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public UserStatus UserStatus { get; init; }
}
