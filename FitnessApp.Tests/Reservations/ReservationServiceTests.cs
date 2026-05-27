using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitnessApp.Tests.Reservations;

public class ReservationServiceTests
{
    [Fact]
    public async Task ReserveAsync_WhenUserHasNoBalance_ShouldCreateReservation()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var response = await reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest
            {
                TrainingSessionId = training.Id,
                Notes = "Dolazim"
            });

        response.UserId.Should().Be(user.Id);
        response.TrainingSessionId.Should().Be(training.Id);
        response.Status.Should().Be(ReservationStatus.Reserved);
        response.Notes.Should().Be("Dolazim");

        var reservation = await dbContext.Reservations.SingleAsync();
        reservation.Status.Should().Be(ReservationStatus.Reserved);
        reservation.ReservedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        dbContext.UserTrainingBalances.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveAsync_WhenUserIsUnverified_ShouldThrowForbidden()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Unverified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Korisnik nije verifikovan.");
    }

    [Fact]
    public async Task ReserveAsync_WhenUserIsBlocked_ShouldThrowForbidden()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Blocked);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Korisnik je blokiran.");
    }

    [Fact]
    public async Task ReserveAsync_WhenTrainingIsCancelled_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        training.IsCancelled = true;
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Trening je otkazan.");
    }

    [Fact]
    public async Task ReserveAsync_WhenTrainingIsInPast_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(-1), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Trening je već počeo ili je završen.");
    }

    [Fact]
    public async Task ReserveAsync_WhenTrainingIsFull_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var otherUser = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 1);
        training.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = otherUser.Id,
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Reserved
        });
        dbContext.Users.AddRange(user, otherUser);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Trening je popunjen.");
    }

    [Fact]
    public async Task ReserveAsync_WhenSameTrainingAlreadyReserved_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        training.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Reserved
        });
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training.Id });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Već imate rezervaciju za ovaj trening.");
    }

    [Fact]
    public async Task ReserveAsync_WhenUserAlreadyHasTwoUpcomingReservations_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training1 = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        var training2 = CreateTraining(DateTime.UtcNow.AddDays(2), capacity: 10);
        var training3 = CreateTraining(DateTime.UtcNow.AddDays(3), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.AddRange(training1, training2, training3);
        dbContext.Reservations.AddRange(
            CreateReservation(user.Id, training1.Id),
            CreateReservation(user.Id, training2.Id));
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.ReserveAsync(
            user.Id,
            new CreateReservationRequest { TrainingSessionId = training3.Id });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Možete imati najviše 2 naredne rezervacije.");
    }

    [Fact]
    public async Task CancelReservationAsync_ShouldCancelActiveUserReservation()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var response = await reservationService.CancelReservationAsync(reservation.Id, user.Id);

        response.Status.Should().Be(ReservationStatus.Cancelled);
        response.CancelledByUser.Should().BeTrue();
        response.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelReservationAsync_WhenCancellationDeadlineHasPassed_ShouldThrowConflict()
    {
        var services = CreateServiceProvider(cancellationDeadlineHours: 12);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddHours(6), capacity: 10);
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.CancelReservationAsync(reservation.Id, user.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Rok za otkazivanje rezervacije je prošao.");
    }

    [Fact]
    public async Task CancelReservationAsync_WhenTrainingHasStarted_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddHours(-1), capacity: 10);
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.CancelReservationAsync(reservation.Id, user.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Trening je već počeo ili je završen.");
    }

    [Fact]
    public async Task CancelReservationAsync_WhenReservationBelongsToAnotherUser_ShouldThrowNotFound()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var otherUser = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        var reservation = CreateReservation(otherUser.Id, training.Id);
        dbContext.Users.AddRange(user, otherUser);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.CancelReservationAsync(reservation.Id, user.Id);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Rezervacija nije pronađena.");
    }

    [Fact]
    public async Task CancelReservationAsync_WhenReservationIsNotReserved_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        var reservation = CreateReservation(user.Id, training.Id);
        reservation.Status = ReservationStatus.Cancelled;
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var act = () => reservationService.CancelReservationAsync(reservation.Id, user.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Rezervacija nije aktivna.");
    }

    [Fact]
    public async Task GetUpcomingReservationsAsync_ShouldReturnOnlyFutureReservedReservations()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var reservationService = services.GetRequiredService<IReservationService>();
        var user = CreateUser(UserStatus.Verified);
        var futureTraining = CreateTraining(DateTime.UtcNow.AddDays(1), capacity: 10);
        var pastTraining = CreateTraining(DateTime.UtcNow.AddDays(-1), capacity: 10);
        var cancelledTraining = CreateTraining(DateTime.UtcNow.AddDays(2), capacity: 10);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.AddRange(futureTraining, pastTraining, cancelledTraining);
        dbContext.Reservations.AddRange(
            CreateReservation(user.Id, futureTraining.Id),
            CreateReservation(user.Id, pastTraining.Id),
            new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TrainingSessionId = cancelledTraining.Id,
                Status = ReservationStatus.Cancelled,
                ReservedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var response = await reservationService.GetUpcomingReservationsAsync(user.Id);

        response.Should().ContainSingle();
        response.Single().TrainingSessionId.Should().Be(futureTraining.Id);
    }

    private static ServiceProvider CreateServiceProvider(int cancellationDeadlineHours = 12)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new AppSettings
        {
            FrontendUrl = "http://localhost:5173",
            CancellationDeadlineHours = cancellationDeadlineHours,
            DefaultTrainingCapacity = 10,
            AutoMarkAttendanceDelayMinutes = 60
        }));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<IReservationService, ReservationService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(UserStatus status)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+381600000000",
            UserStatus = status,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TrainingSession CreateTraining(DateTime startTime, int capacity)
    {
        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Trening",
            Description = string.Empty,
            StartTime = startTime,
            EndTime = startTime.AddHours(1),
            Capacity = capacity,
            TrainerName = "Sara",
            Location = "Studio",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Reservation CreateReservation(Guid userId, Guid trainingSessionId)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrainingSessionId = trainingSessionId,
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow
        };
    }
}
