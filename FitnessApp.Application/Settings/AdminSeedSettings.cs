namespace FitnessApp.Application.Settings;

public sealed class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;
}
