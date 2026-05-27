using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Features.Reservations.Mappings;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class ReservationService : IReservationService
{
    private const int MaxUpcomingReservations = 2;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IBalanceService _balanceService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        AppDbContext dbContext,
        IBalanceService balanceService,
        ISettingsService settingsService,
        ILogger<ReservationService> logger)
    {
        _dbContext = dbContext;
        _balanceService = balanceService;
        _settingsService = settingsService;
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

    public async Task<ReservationResponse> MarkAsAttendedAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);
        ValidateUserId(adminId);

        var reservation = await _dbContext.Reservations
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException("Rezervacija nije pronađena.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new ConflictException("Rezervacija nije aktivna.");
        }

        if (reservation.TrainingSession.StartTime > DateTime.UtcNow)
        {
            throw new ConflictException("Trening još nije počeo.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _balanceService.ConsumeSessionAsync(reservation.UserId, cancellationToken);
        }
        catch (ConflictException)
        {
            throw new ConflictException("Korisnik nema dostupnih termina. Prvo evidentirajte uplatu.");
        }

        reservation.Status = ReservationStatus.Attended;
        reservation.AttendedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Marked reservation {ReservationId} as attended by admin {AdminId}. Session consumed for user {UserId}.",
            reservation.Id,
            adminId,
            reservation.UserId);

        return reservation.ToResponse();
    }

    public async Task<ReservationResponse> MarkAsNoShowAsync(
        Guid reservationId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);
        ValidateUserId(adminId);

        var reservation = await _dbContext.Reservations
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException("Rezervacija nije pronađena.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new ConflictException("Rezervacija nije aktivna.");
        }

        if (reservation.TrainingSession.EndTime > DateTime.UtcNow)
        {
            throw new ConflictException("Trening još nije završen.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _balanceService.ConsumeSessionAsync(reservation.UserId, cancellationToken);
        }
        catch (ConflictException)
        {
            throw new ConflictException("Korisnik nema dostupnih termina. Prvo evidentirajte uplatu.");
        }

        reservation.Status = ReservationStatus.NoShow;
        reservation.NoShowAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await CheckForAutomaticBlockAsync(reservation.UserId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Marked reservation {ReservationId} as no-show by admin {AdminId}. Session consumed for user {UserId}.",
            reservation.Id,
            adminId,
            reservation.UserId);

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

        var cancellationDeadlineHours = await _settingsService.GetCancellationDeadlineHoursAsync(cancellationToken);
        var cancellationDeadline = reservation.TrainingSession.StartTime
            .AddHours(-cancellationDeadlineHours);

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
            .Include(reservation => reservation.User)
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
            .Include(reservation => reservation.User)
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

    public async Task<PaginatedResponse<ReservationResponse>> GetReservationsAsync(
        int page,
        int pageSize,
        DateTime? date = null,
        ReservationStatus? status = null,
        Guid? userId = null,
        Guid? trainingSessionId = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .AsQueryable();

        query = ApplyFilters(query, date, status, userId, trainingSessionId);
        query = ApplySorting(query, sortBy, sortDescending);

        return await GetPaginatedReservationsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<ReservationResponse> GetReservationByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);

        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.TrainingSession)
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException("Rezervacija nije pronađena.");
        }

        return reservation.ToResponse();
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

    private static IQueryable<Reservation> ApplyFilters(
        IQueryable<Reservation> query,
        DateTime? date,
        ReservationStatus? status,
        Guid? userId,
        Guid? trainingSessionId)
    {
        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);

            query = query.Where(reservation =>
                reservation.TrainingSession.StartTime >= dayStart
                && reservation.TrainingSession.StartTime < dayEnd);
        }

        if (status.HasValue)
        {
            query = query.Where(reservation => reservation.Status == status.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(reservation => reservation.UserId == userId.Value);
        }

        if (trainingSessionId.HasValue)
        {
            query = query.Where(reservation => reservation.TrainingSessionId == trainingSessionId.Value);
        }

        return query;
    }

    private static IQueryable<Reservation> ApplySorting(
        IQueryable<Reservation> query,
        string? sortBy,
        bool sortDescending)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "status" => sortDescending
                ? query.OrderByDescending(reservation => reservation.Status)
                    .ThenBy(reservation => reservation.TrainingSession.StartTime)
                : query.OrderBy(reservation => reservation.Status)
                    .ThenBy(reservation => reservation.TrainingSession.StartTime),
            _ => sortDescending
                ? query.OrderByDescending(reservation => reservation.TrainingSession.StartTime)
                    .ThenBy(reservation => reservation.Status)
                : query.OrderBy(reservation => reservation.TrainingSession.StartTime)
                    .ThenBy(reservation => reservation.Status)
        };
    }

    private static async Task<PaginatedResponse<ReservationResponse>> GetPaginatedReservationsAsync(
        IQueryable<Reservation> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var reservations = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = reservations
            .Select(reservation => reservation.ToResponse())
            .ToArray();

        return new PaginatedResponse<ReservationResponse>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
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

    private async Task CheckForAutomaticBlockAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var latestReservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.TrainingSession)
            .Where(reservation =>
                reservation.UserId == userId
                && (reservation.Status == ReservationStatus.Attended
                    || reservation.Status == ReservationStatus.NoShow))
            .OrderByDescending(reservation => reservation.TrainingSession.StartTime)
            .Take(2)
            .Select(reservation => reservation.Status)
            .ToListAsync(cancellationToken);

        var hasTwoConsecutiveNoShows = latestReservations.Count == 2
            && latestReservations.All(status => status == ReservationStatus.NoShow);

        if (!hasTwoConsecutiveNoShows)
        {
            return;
        }

        var currentBalance = await _balanceService.GetCurrentBalanceAsync(userId, cancellationToken);
        var hasNoActivePackageOrAvailableSessions = currentBalance.ActivePackage is null
            && !currentBalance.HasAvailableSessions;

        if (!hasNoActivePackageOrAvailableSessions)
        {
            return;
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (user is null || user.UserStatus == UserStatus.Blocked)
        {
            return;
        }

        user.UserStatus = UserStatus.Blocked;
        user.BlockedAt = DateTime.UtcNow;

        // TODO: Send future notification/email when a user is automatically blocked.
        _logger.LogWarning(
            "Automatically blocked user {UserId} after two consecutive no-show reservations without active package or available sessions.",
            userId);
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
