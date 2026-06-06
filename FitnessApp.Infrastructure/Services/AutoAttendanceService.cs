using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class AutoAttendanceService : IAutoAttendanceService
{
    private readonly AppDbContext _dbContext;
    private readonly IBalanceService _balanceService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AutoAttendanceService> _logger;

    public AutoAttendanceService(
        AppDbContext dbContext,
        IBalanceService balanceService,
        ISettingsService settingsService,
        ILogger<AutoAttendanceService> logger)
    {
        _dbContext = dbContext;
        _balanceService = balanceService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task AutoMarkAttendanceAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var autoMarkDelayMinutes = await _settingsService.GetAutoMarkAttendanceDelayMinutesAsync(cancellationToken);
        var eligibleTrainingEndTime = utcNow.AddMinutes(-autoMarkDelayMinutes);

        var reservations = await _dbContext.Reservations
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.Status == ReservationStatus.Reserved
                && reservation.TrainingSession.EndTime <= eligibleTrainingEndTime)
            .OrderBy(reservation => reservation.TrainingSession.EndTime)
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _balanceService.ConsumeSessionAsync(reservation.UserId, cancellationToken);

                reservation.Status = ReservationStatus.Attended;
                reservation.AttendedAt = utcNow;
                reservation.AutoMarkedAttended = true;
                reservation.AutoMarkedAt = utcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Automatically marked reservation {ReservationId} as attended for user {UserId}.",
                    reservation.Id,
                    reservation.UserId);
            }
            catch (ConflictException)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogWarning(
                    "Automatic attendance failed for reservation {ReservationId} and user {UserId} because no sessions are available.",
                    reservation.Id,
                    reservation.UserId);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "Automatic attendance failed for reservation {ReservationId} and user {UserId}. Continuing with next reservation.",
                    reservation.Id,
                    reservation.UserId);
            }
        }
    }
}
