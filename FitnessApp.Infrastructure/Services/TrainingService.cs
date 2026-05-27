using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Application.Features.Trainings.Mappings;
using FitnessApp.Domain.Enums;
using FitnessApp.Domain.Entities;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class TrainingService : ITrainingService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TrainingService> _logger;

    public TrainingService(
        AppDbContext dbContext,
        INotificationService notificationService,
        ISettingsService settingsService,
        ILogger<TrainingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TrainingCalendarResponse>> GetUpcomingTrainingsAsync(
        DateTime? date = null,
        bool? isCancelled = false,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var query = _dbContext.TrainingSessions
            .AsNoTracking()
            .Include(training => training.Reservations)
            .Where(training => training.StartTime > utcNow)
            .AsQueryable();

        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);

            query = query.Where(training =>
                training.StartTime >= dayStart
                && training.StartTime < dayEnd);
        }

        if (isCancelled.HasValue)
        {
            query = query.Where(training => training.IsCancelled == isCancelled.Value);
        }

        var trainings = await query
            .OrderBy(training => training.StartTime)
            .ToListAsync(cancellationToken);

        return trainings
            .Select(training => training.ToCalendarResponse())
            .ToArray();
    }

    public async Task<TrainingSessionResponse> GetTrainingByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ValidateTrainingId(id);

        var training = await _dbContext.TrainingSessions
            .AsNoTracking()
            .Include(training => training.Reservations)
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken);

        if (training is null)
        {
            throw new NotFoundException("Trening nije pronađen.");
        }

        return training.ToResponse();
    }

    public async Task<TrainingSessionResponse> CreateTrainingAsync(
        CreateTrainingSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTrainingTimes(request.StartTime, request.EndTime);
        ValidateCreateCapacity(request.Capacity);

        var capacity = request.Capacity > 0
            ? request.Capacity
            : await _settingsService.GetDefaultTrainingCapacityAsync(cancellationToken);

        ValidateCapacity(capacity);

        var training = new TrainingSession
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Capacity = capacity,
            TrainerName = string.IsNullOrWhiteSpace(request.TrainerName)
                ? "Sara"
                : request.TrainerName.Trim(),
            Location = request.Location?.Trim() ?? string.Empty,
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.TrainingSessions.Add(training);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created training session {TrainingSessionId} with capacity {Capacity}.",
            training.Id,
            training.Capacity);

        return training.ToResponse();
    }

    public async Task<TrainingSessionResponse> UpdateTrainingAsync(
        Guid id,
        UpdateTrainingSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTrainingId(id);
        ValidateTrainingTimes(request.StartTime, request.EndTime);
        ValidateCapacity(request.Capacity);

        var training = await GetTrackedTrainingAsync(id, cancellationToken);

        if (training.Reservations.Count != 0 && request.StartTime <= DateTime.UtcNow)
        {
            throw new ConflictException("Trening sa rezervacijama ne može biti pomeren u prošlost.");
        }

        training.Title = request.Title.Trim();
        training.Description = request.Description?.Trim() ?? string.Empty;
        training.StartTime = request.StartTime;
        training.EndTime = request.EndTime;
        training.Capacity = request.Capacity;
        training.IsCancelled = request.IsCancelled;
        training.CancellationReason = request.CancellationReason?.Trim();
        training.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated training session {TrainingSessionId}.", training.Id);

        if (training.Reservations.Any(reservation => reservation.Status == ReservationStatus.Reserved))
        {
            await _notificationService.SendTrainingUpdatedNotificationsAsync(training.Id, cancellationToken);
        }

        return training.ToResponse();
    }

    public async Task<TrainingSessionResponse> CancelTrainingAsync(
        Guid id,
        string? cancellationReason = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTrainingId(id);
        ValidateCancellationReason(cancellationReason);

        var training = await GetTrackedTrainingAsync(id, cancellationToken);

        training.IsCancelled = true;
        training.CancellationReason = cancellationReason?.Trim();
        training.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.SendTrainingCancelledNotificationsAsync(
            training.Id,
            training.CancellationReason!,
            cancellationToken);

        _logger.LogInformation("Cancelled training session {TrainingSessionId}.", training.Id);

        return training.ToResponse();
    }

    public async Task DeleteTrainingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ValidateTrainingId(id);

        var training = await _dbContext.TrainingSessions
            .Include(training => training.Reservations)
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken);

        if (training is null)
        {
            throw new NotFoundException("Trening nije pronađen.");
        }

        if (training.Reservations.Count != 0)
        {
            throw new ConflictException("Trening sa rezervacijama ne može biti obrisan.");
        }

        _dbContext.TrainingSessions.Remove(training);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted training session {TrainingSessionId}.", id);
    }

    private async Task<TrainingSession> GetTrackedTrainingAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var training = await _dbContext.TrainingSessions
            .Include(training => training.Reservations)
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken);

        if (training is null)
        {
            throw new NotFoundException("Trening nije pronađen.");
        }

        return training;
    }

    private static void ValidateTrainingId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new BadRequestException("Trening je obavezan.");
        }
    }

    private static void ValidateTrainingTimes(DateTime startTime, DateTime endTime)
    {
        if (startTime == default)
        {
            throw new BadRequestException("Vreme početka je obavezno.");
        }

        if (startTime <= DateTime.UtcNow)
        {
            throw new BadRequestException("Vreme početka mora biti u budućnosti.");
        }

        if (endTime == default)
        {
            throw new BadRequestException("Vreme završetka je obavezno.");
        }

        if (endTime <= startTime)
        {
            throw new BadRequestException("Vreme završetka mora biti nakon vremena početka.");
        }
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new BadRequestException("Kapacitet mora biti veći od 0.");
        }
    }

    private static void ValidateCreateCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new BadRequestException("Kapacitet ne može biti negativan.");
        }
    }

    private static void ValidateCancellationReason(string? cancellationReason)
    {
        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            throw new BadRequestException("Razlog otkazivanja je obavezan.");
        }
    }
}
