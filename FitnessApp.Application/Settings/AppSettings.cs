namespace FitnessApp.Application.Settings;

public sealed class AppSettings
{
    public const string SectionName = "AppSettings";

    public string ContactPhone { get; init; } = string.Empty;

    public string[] AllowedOrigins { get; init; } = [];

    public string FrontendUrl { get; init; } = string.Empty;

    public int CancellationDeadlineHours { get; init; }

    public int DefaultTrainingCapacity { get; init; }

    public int AutoMarkAttendanceDelayMinutes { get; init; }
}
