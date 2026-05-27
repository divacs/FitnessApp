using FitnessApp.Application.Features.Dashboard.Interfaces;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetUserDashboardAsync_ShouldReturnUserBalanceReservationsNotificationsAndExpirationWarning()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var dashboardService = services.GetRequiredService<IDashboardService>();
        var user = CreateUser();
        var activePackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 4,
            StartDate = DateTime.UtcNow.AddDays(-27),
            EndDate = DateTime.UtcNow.AddDays(2),
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-27)
        };
        var singleSessions = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.SingleSessions,
            TotalSessions = 2,
            RemainingSessions = 2,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var laterTraining = CreateTraining(DateTime.UtcNow.AddDays(2), "Kasniji trening");
        var earlierTraining = CreateTraining(DateTime.UtcNow.AddDays(1), "Raniji trening");
        var pastTraining = CreateTraining(DateTime.UtcNow.AddDays(-1), "Prosli trening");
        var latestNotification = CreateNotification("Najnovije", DateTime.UtcNow);
        var olderNotification = CreateNotification("Starije", DateTime.UtcNow.AddHours(-1));

        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(activePackage, singleSessions);
        dbContext.TrainingSessions.AddRange(laterTraining, earlierTraining, pastTraining);
        dbContext.Reservations.AddRange(
            CreateReservation(user.Id, laterTraining.Id, ReservationStatus.Reserved),
            CreateReservation(user.Id, earlierTraining.Id, ReservationStatus.Reserved),
            CreateReservation(user.Id, pastTraining.Id, ReservationStatus.Reserved));
        dbContext.Notifications.AddRange(latestNotification, olderNotification);
        dbContext.UserNotifications.AddRange(
            CreateUserNotification(user.Id, latestNotification.Id, latestNotification.CreatedAt),
            CreateUserNotification(user.Id, olderNotification.Id, olderNotification.CreatedAt));
        await dbContext.SaveChangesAsync();

        var response = await dashboardService.GetUserDashboardAsync(user.Id);

        response.User.Id.Should().Be(user.Id);
        response.User.Email.Should().Be(user.Email);
        response.User.UserStatus.Should().Be(UserStatus.Verified);
        response.ActivePackage.Should().NotBeNull();
        response.MembershipExpiresAt.Should().Be(activePackage.EndDate);
        response.SingleSessionsRemaining.Should().Be(2);
        response.CurrentBalance.TotalRemainingSessions.Should().Be(6);
        response.UpcomingReservations.Select(reservation => reservation.TrainingSessionId)
            .Should()
            .Equal(earlierTraining.Id, laterTraining.Id);
        response.LatestNotifications.Select(notification => notification.Title)
            .Should()
            .Equal("Najnovije", "Starije");
        response.IsMembershipExpiringSoon.Should().BeTrue();
        response.MembershipExpirationWarning.Should().Be("Članarina ističe za 2 dana.");
    }

    [Fact]
    public async Task GetUserDashboardAsync_WhenMembershipIsNotExpiringSoon_ShouldNotReturnWarning()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var dashboardService = services.GetRequiredService<IDashboardService>();
        var user = CreateUser();
        var activePackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package6,
            TotalSessions = 6,
            RemainingSessions = 6,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(activePackage);
        await dbContext.SaveChangesAsync();

        var response = await dashboardService.GetUserDashboardAsync(user.Id);

        response.IsMembershipExpiringSoon.Should().BeFalse();
        response.MembershipExpirationWarning.Should().BeNull();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IDashboardService, DashboardService>();

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

    private static Notification CreateNotification(string title, DateTime createdAt)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            Title = title,
            Message = "Poruka",
            Type = NotificationType.General,
            CreatedAt = createdAt
        };
    }

    private static UserNotification CreateUserNotification(
        Guid userId,
        Guid notificationId,
        DateTime createdAt)
    {
        return new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationId = notificationId,
            CreatedAt = createdAt
        };
    }
}
