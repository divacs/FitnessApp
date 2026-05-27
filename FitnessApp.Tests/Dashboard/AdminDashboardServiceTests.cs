using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Dashboard;

public class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetAdminDashboardAsync_ShouldReturnDashboardOverview()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var dashboardService = services.GetRequiredService<IAdminDashboardService>();
        var verifiedWithSessions = CreateUser(UserStatus.Verified, DateTime.UtcNow.AddDays(-5));
        var verifiedWithoutSessions = CreateUser(UserStatus.Verified, DateTime.UtcNow.AddDays(-4));
        var unverifiedUser = CreateUser(UserStatus.Unverified, DateTime.UtcNow.AddDays(-3));
        var blockedUser = CreateUser(UserStatus.Blocked, DateTime.UtcNow.AddDays(-2));
        var weekTraining = CreateTraining(DateTime.UtcNow.Date.AddDays(1), "Trening ove nedelje");
        var outsideWeekTraining = CreateTraining(DateTime.UtcNow.Date.AddDays(10), "Kasniji trening");
        var latestPayment = CreatePayment(verifiedWithSessions.Id, DateTime.UtcNow);
        var olderPayment = CreatePayment(verifiedWithSessions.Id, DateTime.UtcNow.AddDays(-1));
        var expiringPackage = CreateBalance(
            verifiedWithSessions.Id,
            PurchaseType.Package12,
            remainingSessions: 3,
            endDate: DateTime.UtcNow.AddDays(2));
        var futurePackage = CreateBalance(
            verifiedWithSessions.Id,
            PurchaseType.Package6,
            remainingSessions: 6,
            endDate: DateTime.UtcNow.AddDays(10));
        var latestReservation = CreateReservation(verifiedWithSessions.Id, weekTraining.Id, ReservationStatus.Reserved);
        latestReservation.ReservedAt = DateTime.UtcNow;
        var autoMarkedReservation = CreateReservation(verifiedWithSessions.Id, outsideWeekTraining.Id, ReservationStatus.Attended);
        autoMarkedReservation.AutoMarkedAttended = true;
        autoMarkedReservation.AutoMarkedAt = DateTime.UtcNow;

        dbContext.Users.AddRange(verifiedWithSessions, verifiedWithoutSessions, unverifiedUser, blockedUser);
        dbContext.TrainingSessions.AddRange(weekTraining, outsideWeekTraining);
        dbContext.UserTrainingBalances.AddRange(expiringPackage, futurePackage);
        dbContext.Payments.AddRange(latestPayment, olderPayment);
        dbContext.Reservations.AddRange(latestReservation, autoMarkedReservation);
        await dbContext.SaveChangesAsync();

        var response = await dashboardService.GetAdminDashboardAsync();

        response.TotalUsers.Should().Be(4);
        response.VerifiedUsers.Should().Be(2);
        response.UnverifiedUsers.Should().Be(1);
        response.BlockedUsers.Should().Be(1);
        response.TrainingsThisWeek.Should().Be(1);
        response.ReservationsThisWeek.Should().Be(1);
        response.PendingVerificationUsers.Should().ContainSingle(user => user.Id == unverifiedUser.Id);
        response.UsersWithoutSessions.Should().ContainSingle(user => user.Id == verifiedWithoutSessions.Id);
        response.PackagesExpiringSoon.Should().ContainSingle(balance => balance.Id == expiringPackage.Id);
        response.LatestPayments.First().Id.Should().Be(latestPayment.Id);
        response.LatestReservations.Should().Contain(reservation => reservation.Id == latestReservation.Id);
        response.AutoMarkedAttendances.Should().ContainSingle(reservation => reservation.Id == autoMarkedReservation.Id);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(UserStatus status, DateTime createdAt)
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
            CreatedAt = createdAt
        };
    }

    private static TrainingSession CreateTraining(DateTime startTime, string title)
    {
        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            StartTime = startTime,
            EndTime = startTime.AddHours(1),
            Capacity = 10,
            TrainerName = "Sara",
            Location = "Studio",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Payment CreatePayment(Guid userId, DateTime createdAt)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 1000,
            PaymentDate = createdAt,
            PaymentType = PurchaseType.Package12,
            NumberOfSessions = 12,
            CreatedAt = createdAt
        };
    }

    private static UserTrainingBalance CreateBalance(
        Guid userId,
        PurchaseType purchaseType,
        int remainingSessions,
        DateTime endDate)
    {
        return new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PurchaseType = purchaseType,
            TotalSessions = purchaseType == PurchaseType.Package6 ? 6 : 12,
            RemainingSessions = remainingSessions,
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = endDate,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
    }

    private static Reservation CreateReservation(
        Guid userId,
        Guid trainingSessionId,
        ReservationStatus status)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrainingSessionId = trainingSessionId,
            Status = status,
            ReservedAt = DateTime.UtcNow
        };
    }
}
