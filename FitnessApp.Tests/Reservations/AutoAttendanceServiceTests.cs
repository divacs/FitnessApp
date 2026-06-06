using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitnessApp.Tests.Reservations;

public class AutoAttendanceServiceTests
{
    [Fact]
    public async Task AutoMarkAttendanceAsync_WhenReservationIsEligibleAndUserHasSession_ShouldMarkAttended()
    {
        var services = CreateServiceProvider(autoMarkAttendanceDelayMinutes: 60);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var autoAttendanceService = services.GetRequiredService<IAutoAttendanceService>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(-3));
        var reservation = CreateReservation(user.Id, training.Id);
        var balance = CreateBalance(user.Id, remainingSessions: 1);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await autoAttendanceService.AutoMarkAttendanceAsync();

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.Status.Should().Be(ReservationStatus.Attended);
        updatedReservation.AttendedAt.Should().NotBeNull();
        updatedReservation.AutoMarkedAttended.Should().BeTrue();
        updatedReservation.AutoMarkedAt.Should().NotBeNull();

        var updatedBalance = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == balance.Id);
        updatedBalance.RemainingSessions.Should().Be(0);
    }

    [Fact]
    public async Task AutoMarkAttendanceAsync_WhenUserHasNoSessions_ShouldLeaveReservationReserved()
    {
        var services = CreateServiceProvider(autoMarkAttendanceDelayMinutes: 60);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var autoAttendanceService = services.GetRequiredService<IAutoAttendanceService>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(-3));
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await autoAttendanceService.AutoMarkAttendanceAsync();

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.Status.Should().Be(ReservationStatus.Reserved);
        updatedReservation.AttendedAt.Should().BeNull();
        updatedReservation.NoShowAt.Should().BeNull();
        updatedReservation.AutoMarkedAttended.Should().BeFalse();
    }

    [Fact]
    public async Task AutoMarkAttendanceAsync_WhenDelayHasNotPassed_ShouldLeaveReservationReserved()
    {
        var services = CreateServiceProvider(autoMarkAttendanceDelayMinutes: 60);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var autoAttendanceService = services.GetRequiredService<IAutoAttendanceService>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddMinutes(-30));
        var reservation = CreateReservation(user.Id, training.Id);
        var balance = CreateBalance(user.Id, remainingSessions: 1);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await autoAttendanceService.AutoMarkAttendanceAsync();

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.Status.Should().Be(ReservationStatus.Reserved);
        updatedReservation.AutoMarkedAttended.Should().BeFalse();

        var updatedBalance = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == balance.Id);
        updatedBalance.RemainingSessions.Should().Be(1);
    }

    [Fact]
    public async Task AutoMarkAttendanceAsync_ShouldProcessOnlyReservedReservations_AndNeverMarkNoShow()
    {
        var services = CreateServiceProvider(autoMarkAttendanceDelayMinutes: 60);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var autoAttendanceService = services.GetRequiredService<IAutoAttendanceService>();
        var user = CreateUser();
        var reservedTraining = CreateTraining(DateTime.UtcNow.AddHours(-3));
        var attendedTraining = CreateTraining(DateTime.UtcNow.AddHours(-4));
        var noShowTraining = CreateTraining(DateTime.UtcNow.AddHours(-5));
        var reservedReservation = CreateReservation(user.Id, reservedTraining.Id);
        var attendedReservation = CreateReservation(user.Id, attendedTraining.Id);
        attendedReservation.Status = ReservationStatus.Attended;
        attendedReservation.AttendedAt = DateTime.UtcNow.AddHours(-3);
        var noShowReservation = CreateReservation(user.Id, noShowTraining.Id);
        noShowReservation.Status = ReservationStatus.NoShow;
        noShowReservation.NoShowAt = DateTime.UtcNow.AddHours(-4);
        var balance = CreateBalance(user.Id, remainingSessions: 1);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.AddRange(reservedTraining, attendedTraining, noShowTraining);
        dbContext.Reservations.AddRange(reservedReservation, attendedReservation, noShowReservation);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await autoAttendanceService.AutoMarkAttendanceAsync();

        var updatedReservedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservedReservation.Id);
        var updatedAttendedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == attendedReservation.Id);
        var updatedNoShowReservation = await dbContext.Reservations.SingleAsync(x => x.Id == noShowReservation.Id);

        updatedReservedReservation.Status.Should().Be(ReservationStatus.Attended);
        updatedReservedReservation.NoShowAt.Should().BeNull();
        updatedAttendedReservation.Status.Should().Be(ReservationStatus.Attended);
        updatedAttendedReservation.AutoMarkedAttended.Should().BeFalse();
        updatedNoShowReservation.Status.Should().Be(ReservationStatus.NoShow);
        updatedNoShowReservation.AutoMarkedAttended.Should().BeFalse();
        updatedNoShowReservation.NoShowAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AutoMarkAttendanceAsync_WhenOneReservationFails_ShouldContinueProcessingOtherReservedReservations()
    {
        var failingUserId = Guid.NewGuid();
        var fakeBalanceService = new FakeBalanceService();
        fakeBalanceService.FailForUserIds.Add(failingUserId);
        var services = CreateServiceProvider(autoMarkAttendanceDelayMinutes: 60, balanceService: fakeBalanceService);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var autoAttendanceService = services.GetRequiredService<IAutoAttendanceService>();
        var failingUser = CreateUser(failingUserId);
        var successfulUser = CreateUser();
        var failingTraining = CreateTraining(DateTime.UtcNow.AddHours(-3));
        var successfulTraining = CreateTraining(DateTime.UtcNow.AddHours(-4));
        var failingReservation = CreateReservation(failingUser.Id, failingTraining.Id);
        var successfulReservation = CreateReservation(successfulUser.Id, successfulTraining.Id);
        dbContext.Users.AddRange(failingUser, successfulUser);
        dbContext.TrainingSessions.AddRange(failingTraining, successfulTraining);
        dbContext.Reservations.AddRange(failingReservation, successfulReservation);
        await dbContext.SaveChangesAsync();

        await autoAttendanceService.AutoMarkAttendanceAsync();

        var updatedFailingReservation = await dbContext.Reservations.SingleAsync(x => x.Id == failingReservation.Id);
        var updatedSuccessfulReservation = await dbContext.Reservations.SingleAsync(x => x.Id == successfulReservation.Id);
        updatedFailingReservation.Status.Should().Be(ReservationStatus.Reserved);
        updatedSuccessfulReservation.Status.Should().Be(ReservationStatus.Attended);
        fakeBalanceService.ConsumedUserIds.Should().Contain(successfulUser.Id);
    }

    private static ServiceProvider CreateServiceProvider(
        int autoMarkAttendanceDelayMinutes,
        IBalanceService? balanceService = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new AppSettings
        {
            FrontendUrl = "http://localhost:5173",
            CancellationDeadlineHours = 12,
            DefaultTrainingCapacity = 10,
            AutoMarkAttendanceDelayMinutes = autoMarkAttendanceDelayMinutes
        }));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });

        if (balanceService is null)
        {
            services.AddScoped<IBalanceService, BalanceService>();
        }
        else
        {
            services.AddSingleton(balanceService);
        }

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAutoAttendanceService, AutoAttendanceService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(Guid? userId = null)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId ?? Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+381600000000",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TrainingSession CreateTraining(DateTime endTime)
    {
        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Trening",
            Description = string.Empty,
            StartTime = endTime.AddHours(-1),
            EndTime = endTime,
            Capacity = 10,
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

    private static UserTrainingBalance CreateBalance(Guid userId, int remainingSessions)
    {
        return new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PurchaseType = PurchaseType.SingleSessions,
            TotalSessions = remainingSessions,
            RemainingSessions = remainingSessions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    private sealed class FakeBalanceService : IBalanceService
    {
        public HashSet<Guid> FailForUserIds { get; } = new();
        public List<Guid> ConsumedUserIds { get; } = new();

        public Task<CurrentBalanceResponse> GetCurrentBalanceAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<BalanceHistoryResponse>> GetBalanceHistoryAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<UserTrainingBalanceResponse>> GetUserBalancesAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserTrainingBalanceResponse> CreatePackage12Async(Guid userId, CreatePackage12Request request, Guid adminId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserTrainingBalanceResponse> CreatePackage6Async(Guid userId, CreatePackage6Request request, Guid adminId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserTrainingBalanceResponse> AddSingleSessionsAsync(Guid userId, AddSingleSessionsRequest request, Guid adminId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ApplyCarryOverAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserTrainingBalanceResponse> UpdateBalanceAsync(Guid balanceId, UpdateBalanceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteBalanceAsync(Guid balanceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task ConsumeSessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (FailForUserIds.Contains(userId))
            {
                throw new InvalidOperationException("Simulated balance failure.");
            }

            ConsumedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }
}
