using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitnessApp.Tests.Notifications;

public class NotificationEmailJobTests
{
    [Fact]
    public async Task SendAsync_WhenUserIsVerified_ShouldSendEmail()
    {
        await using var dbContext = CreateDbContext();
        var emailService = new FakeEmailService();
        var user = CreateUser(UserStatus.Verified);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var job = new NotificationEmailJob(emailService, dbContext, NullLogger<NotificationEmailJob>.Instance);

        await job.SendAsync(user.Id, "Obaveštenje", "Nova poruka.");

        emailService.SentEmails.Should().ContainSingle(user.Email);
    }

    [Theory]
    [InlineData(UserStatus.Unverified)]
    [InlineData(UserStatus.Blocked)]
    public async Task SendAsync_WhenUserIsNotVerified_ShouldNotSendEmail(UserStatus userStatus)
    {
        await using var dbContext = CreateDbContext();
        var emailService = new FakeEmailService();
        var user = CreateUser(userStatus);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var job = new NotificationEmailJob(emailService, dbContext, NullLogger<NotificationEmailJob>.Instance);

        await job.SendAsync(user.Id, "Obaveštenje", "Nova poruka.");

        emailService.SentEmails.Should().BeEmpty();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static ApplicationUser CreateUser(UserStatus userStatus)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            UserStatus = userStatus,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<string> SentEmails { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, string plainTextBody, CancellationToken cancellationToken = default)
        {
            SentEmails.Add(toEmail);
            return Task.CompletedTask;
        }

        public Task SendRegistrationPendingApprovalEmailAsync(string toEmail, string firstName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendUserVerifiedEmailAsync(string toEmail, string firstName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMembershipExpiringEmailAsync(string toEmail, string firstName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
