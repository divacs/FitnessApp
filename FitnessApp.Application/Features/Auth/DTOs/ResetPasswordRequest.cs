using System.Text.Json.Serialization;

namespace FitnessApp.Application.Features.Auth.DTOs;

public class ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;

    public string ResetToken { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    [JsonPropertyName("confirmNewPassword")]
    public string ConfirmNewPassword { get; init; } = string.Empty;

    [JsonIgnore]
    public string PasswordConfirmation =>
        !string.IsNullOrWhiteSpace(ConfirmPassword)
            ? ConfirmPassword
            : ConfirmNewPassword;
}
