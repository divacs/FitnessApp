namespace FitnessApp.Application.Settings;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; }

    public int RefreshTokenExpirationDays { get; init; }
}
