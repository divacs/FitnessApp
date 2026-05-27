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

    private static ServiceProvider CreateServiceProvider(int autoMarkAttendanceDelayMinutes)
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
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAutoAttendanceService, AutoAttendanceService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser()
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
}
