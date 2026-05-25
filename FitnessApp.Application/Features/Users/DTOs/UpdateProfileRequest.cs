namespace FitnessApp.Application.Features.Users.DTOs;

public class UpdateProfileRequest
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;
}
