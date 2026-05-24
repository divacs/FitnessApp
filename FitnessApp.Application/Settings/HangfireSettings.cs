namespace FitnessApp.Application.Settings;

public sealed class HangfireSettings
{
    public const string SectionName = "HangfireSettings";

    public string DashboardPath { get; init; } = "/hangfire";
}
