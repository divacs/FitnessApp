namespace FitnessApp.Application.Features.Settings.DTOs;

public class SettingsResponse
{
    public int CancellationDeadlineHours { get; set; }

    public string ContactPhone { get; set; } = string.Empty;

    public int DefaultTrainingCapacity { get; set; }

    public int AutoMarkAttendanceDelayMinutes { get; set; }
}
