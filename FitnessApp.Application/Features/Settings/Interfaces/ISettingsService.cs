using FitnessApp.Application.Features.Settings.DTOs;

namespace FitnessApp.Application.Features.Settings.Interfaces;

public interface ISettingsService
{
    Task<SettingsResponse> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<SettingsResponse> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<int> GetCancellationDeadlineHoursAsync(CancellationToken cancellationToken = default);

    Task<int> GetDefaultTrainingCapacityAsync(CancellationToken cancellationToken = default);

    Task<int> GetAutoMarkAttendanceDelayMinutesAsync(CancellationToken cancellationToken = default);
}
