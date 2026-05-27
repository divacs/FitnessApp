using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Settings.DTOs;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Entities;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace FitnessApp.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string CancellationDeadlineHoursKey = nameof(SettingsResponse.CancellationDeadlineHours);
    private const string ContactPhoneKey = nameof(SettingsResponse.ContactPhone);
    private const string DefaultTrainingCapacityKey = nameof(SettingsResponse.DefaultTrainingCapacity);
    private const string AutoMarkAttendanceDelayMinutesKey = nameof(SettingsResponse.AutoMarkAttendanceDelayMinutes);

    private readonly AppDbContext _dbContext;
    private readonly AppSettings _fallbackSettings;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        AppDbContext dbContext,
        IOptions<AppSettings> fallbackSettings,
        ILogger<SettingsService> logger)
    {
        _dbContext = dbContext;
        _fallbackSettings = fallbackSettings.Value;
        _logger = logger;
    }

    public async Task<SettingsResponse> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        return new SettingsResponse
        {
            CancellationDeadlineHours = GetIntSetting(
                settings,
                CancellationDeadlineHoursKey,
                _fallbackSettings.CancellationDeadlineHours),
            ContactPhone = GetStringSetting(
                settings,
                ContactPhoneKey,
                _fallbackSettings.ContactPhone),
            DefaultTrainingCapacity = GetIntSetting(
                settings,
                DefaultTrainingCapacityKey,
                _fallbackSettings.DefaultTrainingCapacity),
            AutoMarkAttendanceDelayMinutes = GetIntSetting(
                settings,
                AutoMarkAttendanceDelayMinutesKey,
                _fallbackSettings.AutoMarkAttendanceDelayMinutes)
        };
    }

    public async Task<SettingsResponse> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var settings = await _dbContext.AppSettings
            .Where(setting =>
                setting.Key == CancellationDeadlineHoursKey
                || setting.Key == ContactPhoneKey
                || setting.Key == DefaultTrainingCapacityKey
                || setting.Key == AutoMarkAttendanceDelayMinutesKey)
            .ToDictionaryAsync(setting => setting.Key, cancellationToken);

        UpsertSetting(settings, CancellationDeadlineHoursKey, request.CancellationDeadlineHours.ToString(CultureInfo.InvariantCulture));
        UpsertSetting(settings, ContactPhoneKey, request.ContactPhone.Trim());
        UpsertSetting(settings, DefaultTrainingCapacityKey, request.DefaultTrainingCapacity.ToString(CultureInfo.InvariantCulture));
        UpsertSetting(settings, AutoMarkAttendanceDelayMinutesKey, request.AutoMarkAttendanceDelayMinutes.ToString(CultureInfo.InvariantCulture));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application settings were updated.");

        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<int> GetCancellationDeadlineHoursAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        return settings.CancellationDeadlineHours;
    }

    public async Task<int> GetAutoMarkAttendanceDelayMinutesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        return settings.AutoMarkAttendanceDelayMinutes;
    }

    private void UpsertSetting(
        IReadOnlyDictionary<string, AppSetting> existingSettings,
        string key,
        string value)
    {
        if (existingSettings.TryGetValue(key, out var setting))
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            return;
        }

        _dbContext.AppSettings.Add(new AppSetting
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static int GetIntSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int fallback)
    {
        return settings.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
                ? parsedValue
                : fallback;
    }

    private static string GetStringSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string fallback)
    {
        return settings.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static void ValidateRequest(UpdateSettingsRequest request)
    {
        if (request.CancellationDeadlineHours < 0)
        {
            throw new BadRequestException("Rok za otkazivanje ne može biti negativan.");
        }

        if (request.DefaultTrainingCapacity <= 0)
        {
            throw new BadRequestException("Podrazumevani kapacitet treninga mora biti veći od 0.");
        }

        if (request.AutoMarkAttendanceDelayMinutes < 0)
        {
            throw new BadRequestException("Delay za automatsko označavanje dolaska ne može biti negativan.");
        }
    }
}
