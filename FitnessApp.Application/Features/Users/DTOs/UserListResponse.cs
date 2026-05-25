using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Users.DTOs;

public class UserListResponse
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public UserStatus UserStatus { get; init; }

    public DateTime? VerifiedAt { get; init; }

    public DateTime? BlockedAt { get; init; }

    public DateTime? UnblockedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}
