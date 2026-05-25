namespace FitnessApp.Application.Features.Users.DTOs;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}
