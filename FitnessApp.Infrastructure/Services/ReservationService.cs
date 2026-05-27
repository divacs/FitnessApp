using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Features.Reservations.Mappings;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FitnessApp.Infrastructure.Services;

public class ReservationService : IReservationService
{
    private const int MaxUpcomingReservations = 2;

    private readonly AppDbContext _dbContext;
    private readonly AppSettings _appSettings;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        AppDbContext dbContext,
        IOptions<AppSettings> appSettings,
        ILogger<ReservationService> logger)
    {
        _dbContext = dbContext;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task<ReservationResponse> ReserveAsync(
        Guid userId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        if (request.TrainingSessionId == Guid.Empty)
        {
            throw new BadRequestException("Trening je obavezan.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        EnsureUserCanReserve(user);

        var training = await _dbContext.TrainingSessions
            .Include(training => training.Reservations)
            .FirstOrDefaultAsync(training => training.Id == request.TrainingSessionId, cancellationToken);

        if (training is null)
        {
            throw new NotFoundException("Trening nije pronađen.");
        }

        EnsureTrainingCanBeReserved(training);
        await EnsureReservationLimitsAsync(userId, training.Id, cancellationToken);

        var reservation = new Reservation
        {
            UserId = userId,
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            Notes = request.Notes,
            TrainingSession = training
        };

        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created reservation {ReservationId} for user {UserId} and training {TrainingSessionId}.",
            reservation.Id,
            userId,
            training.Id);

        return reservation.ToResponse();
    }

    public async Task<ReservationResponse> CancelReservationAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);
        ValidateUserId(userId);

        var reservation = await _dbContext.Reservations
            .Include(reservation => reservation.TrainingSession)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId && reservation.UserId == userId,
                cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException("Rezervacija nije pronađena.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new ConflictException("Rezervacija nije aktivna.");
        }

        var utcNow = DateTime.UtcNow;

        if (reservation.TrainingSession.StartTime <= utcNow)
        {
            throw new ConflictException("Trening je već počeo ili je završen.");
        }

        var cancellationDeadline = reservation.TrainingSession.StartTime
            .AddHours(-_appSettings.CancellationDeadlineHours);

        if (utcNow > cancellationDeadline)
        {
            throw new ConflictException("Rok za otkazivanje rezervacije je prošao.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = utcNow;
        reservation.CancelledByUser = true;
        reservation.CancelledByAdmin = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cancelled reservation {ReservationId} for user {UserId}.",
            reservation.Id,
            userId);

        return reservation.ToResponse();
    }

    public async Task<IReadOnlyCollection<ReservationResponse>> GetMyReservationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        await EnsureUserExistsAsync(userId, cancellationToken);

        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.ReservedAt)
            .ToListAsync(cancellationToken);

        return reservations
            .Select(reservation => reservation.ToResponse())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ReservationResponse>> GetUpcomingReservationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        await EnsureUserExistsAsync(userId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.UserId == userId
                && reservation.Status == ReservationStatus.Reserved
                && reservation.TrainingSession.StartTime > utcNow)
            .OrderBy(reservation => reservation.TrainingSession.StartTime)
            .ToListAsync(cancellationToken);

        return reservations
            .Select(reservation => reservation.ToResponse())
            .ToArray();
    }

    private async Task EnsureReservationLimitsAsync(
        Guid userId,
        Guid trainingSessionId,
        CancellationToken cancellationToken)
    {
        var hasSameTrainingReservation = await _dbContext.Reservations
            .AsNoTracking()
            .AnyAsync(
                reservation =>
                    reservation.UserId == userId
                    && reservation.TrainingSessionId == trainingSessionId
                    && reservation.Status == ReservationStatus.Reserved,
                cancellationToken);

        if (hasSameTrainingReservation)
        {
            throw new ConflictException("Već imate rezervaciju za ovaj trening.");
        }

        var utcNow = DateTime.UtcNow;
        var upcomingReservationCount = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.TrainingSession)
            .CountAsync(
                reservation =>
                    reservation.UserId == userId
                    && reservation.Status == ReservationStatus.Reserved
                    && reservation.TrainingSession.StartTime > utcNow,
                cancellationToken);

        if (upcomingReservationCount >= MaxUpcomingReservations)
        {
            throw new ConflictException("Možete imati najviše 2 naredne rezervacije.");
        }
    }

    private async Task EnsureUserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (!userExists)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }
    }

    private static void EnsureUserCanReserve(ApplicationUser user)
    {
        if (user.UserStatus == UserStatus.Blocked)
        {
            throw new ForbiddenException("Korisnik je blokiran.");
        }

        if (user.UserStatus != UserStatus.Verified)
        {
            throw new ForbiddenException("Korisnik nije verifikovan.");
        }
    }

    private static void EnsureTrainingCanBeReserved(TrainingSession training)
    {
        if (training.IsCancelled)
        {
            throw new ConflictException("Trening je otkazan.");
        }

        if (training.StartTime <= DateTime.UtcNow)
        {
            throw new ConflictException("Trening je već počeo ili je završen.");
        }

        var reservedCount = training.Reservations
            .Count(reservation => reservation.Status == ReservationStatus.Reserved);

        if (reservedCount >= training.Capacity)
        {
            throw new ConflictException("Trening je popunjen.");
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new BadRequestException("Korisnik je obavezan.");
        }
    }

    private static void ValidateReservationId(Guid reservationId)
    {
        if (reservationId == Guid.Empty)
        {
            throw new BadRequestException("Rezervacija je obavezna.");
        }
    }
}
