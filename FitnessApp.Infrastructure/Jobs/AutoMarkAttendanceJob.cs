using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FitnessApp.Infrastructure.Jobs;

public class AutoMarkAttendanceJob
{
    private readonly IAutoAttendanceService _autoAttendanceService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<AutoMarkAttendanceJob> _logger;

    public AutoMarkAttendanceJob(
        IAutoAttendanceService autoAttendanceService,
        IOptions<AppSettings> appSettings,
        ILogger<AutoMarkAttendanceJob> logger)
    {
        _autoAttendanceService = autoAttendanceService;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            _logger.LogInformation(
                "Starting auto attendance job. Delay minutes: {AutoMarkAttendanceDelayMinutes}.",
                _appSettings.AutoMarkAttendanceDelayMinutes);

            await _autoAttendanceService.AutoMarkAttendanceAsync();

            _logger.LogInformation("Auto attendance job completed successfully.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Auto attendance job failed.");
            throw;
        }
    }
}
