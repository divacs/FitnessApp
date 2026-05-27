using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task SendGlobalNotificationAsync_ShouldCreateUserNotificationsForVerifiedUsersOnly()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var verifiedUser = CreateUser(UserStatus.Verified);
        var unverifiedUser = CreateUser(UserStatus.Unverified);
        var blockedUser = CreateUser(UserStatus.Blocked);
        dbContext.Users.AddRange(verifiedUser, unverifiedUser, blockedUser);
        await dbContext.SaveChangesAsync();

        var response = await notificationService.SendGlobalNotificationAsync(
            CreateRequest(sendEmail: false),
            Guid.NewGuid());

        response.Title.Should().Be("Obaveštenje");

        var userNotifications = await dbContext.UserNotifications.ToListAsync();
        userNotifications.Should().ContainSingle();
        userNotifications.Single().UserId.Should().Be(verifiedUser.Id);
    }

    [Fact]
    public async Task SendGlobalNotificationAsync_WhenSendEmailIsTrue_ShouldEnqueueEmailJobs()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var backgroundJobClient = services.GetRequiredService<FakeBackgroundJobClient>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var firstUser = CreateUser(UserStatus.Verified);
        var secondUser = CreateUser(UserStatus.Verified);
        dbContext.Users.AddRange(firstUser, secondUser);
        await dbContext.SaveChangesAsync();

        await notificationService.SendGlobalNotificationAsync(
            CreateRequest(sendEmail: true),
            Guid.NewGuid());

        backgroundJobClient.CreatedJobs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_WhenUnreadOnlyAndTypeFilterAreApplied_ShouldReturnMatchingNotifications()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var user = CreateUser(UserStatus.Verified);
        var matchingNotification = CreateNotification(NotificationType.System);
        var readNotification = CreateNotification(NotificationType.System);
        var otherTypeNotification = CreateNotification(NotificationType.General);
        dbContext.Users.Add(user);
        dbContext.Notifications.AddRange(matchingNotification, readNotification, otherTypeNotification);
        dbContext.UserNotifications.AddRange(
            CreateUserNotification(user.Id, matchingNotification.Id, isRead: false),
            CreateUserNotification(user.Id, readNotification.Id, isRead: true),
            CreateUserNotification(user.Id, otherTypeNotification.Id, isRead: false));
        await dbContext.SaveChangesAsync();

        var response = await notificationService.GetMyNotificationsAsync(
            user.Id,
            page: 1,
            pageSize: 20,
            unreadOnly: true,
            type: NotificationType.System);

        response.TotalCount.Should().Be(1);
        response.Items.Single().Id.Should().Be(matchingNotification.Id);
        response.Items.Single().IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldMarkOwnedNotificationAsRead()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var user = CreateUser(UserStatus.Verified);
        var notification = CreateNotification(NotificationType.General);
        var userNotification = CreateUserNotification(user.Id, notification.Id, isRead: false);
        dbContext.Users.Add(user);
        dbContext.Notifications.Add(notification);
        dbContext.UserNotifications.Add(userNotification);
        await dbContext.SaveChangesAsync();

        await notificationService.MarkAsReadAsync(user.Id, userNotification.Id);

        var updatedUserNotification = await dbContext.UserNotifications.SingleAsync(x => x.Id == userNotification.Id);
        updatedUserNotification.IsRead.Should().BeTrue();
        updatedUserNotification.ReadAt.Should().NotBeNull();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddSingleton<FakeBackgroundJobClient>();
        services.AddSingleton<IBackgroundJobClient>(provider => provider.GetRequiredService<FakeBackgroundJobClient>());
        services.AddScoped<INotificationService, NotificationService>();

        return services.BuildServiceProvider();
    }

    private static CreateNotificationRequest CreateRequest(bool sendEmail)
    {
        return new CreateNotificationRequest
        {
            Title = "Obaveštenje",
            Message = "Nova poruka za korisnike.",
            Type = NotificationType.General,
            SendEmail = sendEmail
        };
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

    private static Notification CreateNotification(NotificationType type)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            Title = "Obaveštenje",
            Message = "Poruka",
            Type = type,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static UserNotification CreateUserNotification(
        Guid userId,
        Guid notificationId,
        bool isRead)
    {
        return new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationId = notificationId,
            IsRead = isRead,
            ReadAt = isRead ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> CreatedJobs { get; } = new();

        public string Create(Job job, IState state)
        {
            CreatedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string? expectedState)
        {
            return true;
        }
    }
}
