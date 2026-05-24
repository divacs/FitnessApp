namespace FitnessApp.Application.Settings;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; }

    public string SmtpUsername { get; init; } = string.Empty;

    public string SmtpPassword { get; init; } = string.Empty;

    public string FromEmail { get; init; } = string.Empty;

    public string FromName { get; init; } = string.Empty;
}
