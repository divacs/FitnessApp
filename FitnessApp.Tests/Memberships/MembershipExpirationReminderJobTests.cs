using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Memberships;

public class MembershipExpirationReminderJobTests
{
    [Theory]
    [InlineData(PurchaseType.Package12)]
    [InlineData(PurchaseType.Package6)]
    public async Task ExecuteAsync_WhenMonthlyPackageExpiresInThreeDays_ShouldSendReminderAndSetTimestamp(
        PurchaseType purchaseType)
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<MembershipExpirationReminderJob>();
        var user = CreateUser();
        var balance = CreateBalance(user.Id, purchaseType, DateTime.UtcNow.AddDays(3).Date.AddHours(10));
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.MembershipExpirationEmails.Should().ContainSingle();

        var updatedBalance = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == balance.Id);
        updatedBalance.ExpirationReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenBalanceIsSingleSessions_ShouldNotSendReminder()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<MembershipExpirationReminderJob>();
        var user = CreateUser();
        var balance = CreateBalance(user.Id, PurchaseType.SingleSessions, null);
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.MembershipExpirationEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReminderWasAlreadySent_ShouldNotSendReminderAgain()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<MembershipExpirationReminderJob>();
        var user = CreateUser();
        var balance = CreateBalance(user.Id, PurchaseType.Package12, DateTime.UtcNow.AddDays(3).Date.AddHours(10));
        balance.ExpirationReminderSentAt = DateTime.UtcNow.AddDays(-1);
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.MembershipExpirationEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPackageExpiresOutsideThreeDayWindow_ShouldNotSendReminder()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<MembershipExpirationReminderJob>();
        var user = CreateUser();
        var balance = CreateBalance(user.Id, PurchaseType.Package12, DateTime.UtcNow.AddDays(4).Date.AddHours(10));
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.MembershipExpirationEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSendingOneReminderFails_ShouldContinueProcessingOtherBalances()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<MembershipExpirationReminderJob>();
        var failingUser = CreateUser();
        var successfulUser = CreateUser();
        var failingBalance = CreateBalance(failingUser.Id, PurchaseType.Package12, DateTime.UtcNow.AddDays(3).Date.AddHours(10));
        var successfulBalance = CreateBalance(successfulUser.Id, PurchaseType.Package6, DateTime.UtcNow.AddDays(3).Date.AddHours(12));
        emailService.FailForEmails.Add(failingUser.Email!);
        dbContext.Users.AddRange(failingUser, successfulUser);
        dbContext.UserTrainingBalances.AddRange(failingBalance, successfulBalance);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.MembershipExpirationEmails.Should().ContainSingle(successfulUser.Email);

        var updatedFailingBalance = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == failingBalance.Id);
        var updatedSuccessfulBalance = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == successfulBalance.Id);
        updatedFailingBalance.ExpirationReminderSentAt.Should().BeNull();
        updatedSuccessfulBalance.ExpirationReminderSentAt.Should().NotBeNull();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<FakeEmailService>();
        services.AddScoped<IEmailService>(provider => provider.GetRequiredService<FakeEmailService>());
        services.AddScoped<MembershipExpirationReminderJob>();

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

    private static UserTrainingBalance CreateBalance(
        Guid userId,
        PurchaseType purchaseType,
        DateTime? endDate)
    {
        return new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PurchaseType = purchaseType,
            TotalSessions = purchaseType == PurchaseType.Package6 ? 6 : 12,
            RemainingSessions = 1,
            StartDate = DateTime.UtcNow.AddDays(-27),
            EndDate = endDate,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-27)
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<string> MembershipExpirationEmails { get; } = new();
        public HashSet<string> FailForEmails { get; } = new();

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string plainTextBody,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendRegistrationPendingApprovalEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendUserVerifiedEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendMembershipExpiringEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            if (FailForEmails.Contains(toEmail))
            {
                throw new InvalidOperationException("Simulated email failure.");
            }

            MembershipExpirationEmails.Add(toEmail);
            return Task.CompletedTask;
        }
    }
}
