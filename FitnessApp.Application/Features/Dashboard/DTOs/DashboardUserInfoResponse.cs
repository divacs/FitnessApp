using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class DashboardUserInfoResponse
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public UserStatus UserStatus { get; init; }
}
