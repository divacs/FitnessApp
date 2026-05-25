using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Users;

public class UserServiceTests
{
    [Fact]
    public async Task VerifyUserAsync_ShouldSetVerifiedStatusAndSendEmail()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var userService = services.GetRequiredService<IUserService>();
        var user = CreateUser(UserStatus.Unverified);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        await userService.VerifyUserAsync(user.Id);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        updatedUser.UserStatus.Should().Be(UserStatus.Verified);
        updatedUser.VerifiedAt.Should().NotBeNull();
        updatedUser.UpdatedAt.Should().NotBeNull();
        emailService.VerifiedEmails.Should().ContainSingle(x => x.ToEmail == user.Email);
    }

    [Fact]
    public async Task BlockAndUnblockUserAsync_ShouldUpdateStatusFlow()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var userService = services.GetRequiredService<IUserService>();
        var user = CreateUser(UserStatus.Verified);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        await userService.BlockUserAsync(user.Id);

        var blockedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        blockedUser.UserStatus.Should().Be(UserStatus.Blocked);
        blockedUser.BlockedAt.Should().NotBeNull();

        await userService.UnblockUserAsync(user.Id);

        var unblockedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        unblockedUser.UserStatus.Should().Be(UserStatus.Verified);
        unblockedUser.UnblockedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUsersAsync_ShouldFilterByStatusAndSearchAndReturnPagination()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var userService = services.GetRequiredService<IUserService>();
        dbContext.Users.AddRange(
            CreateUser(UserStatus.Unverified, "Ana", "Markovic", "ana@example.com"),
            CreateUser(UserStatus.Verified, "Sara", "Petrovic", "sara@example.com"),
            CreateUser(UserStatus.Verified, "Mila", "Nikolic", "mila@example.com"));
        await dbContext.SaveChangesAsync();

        var result = await userService.GetUsersAsync(
            page: 1,
            pageSize: 1,
            status: UserStatus.Verified,
            search: "a");

        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items.Single().UserStatus.Should().Be(UserStatus.Verified);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });

        services.AddSingleton<FakeEmailService>();
        services.AddSingleton<IEmailService>(provider => provider.GetRequiredService<FakeEmailService>());
        services.AddScoped<IUserService, UserService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(
        UserStatus status,
        string firstName = "Test",
        string lastName = "User",
        string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = "+381600000000",
            UserStatus = status,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string ToEmail, string FirstName)> VerifiedEmails { get; } = new();

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
            VerifiedEmails.Add((toEmail, firstName));

            return Task.CompletedTask;
        }

        public Task SendMembershipExpiringEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
