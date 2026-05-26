using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Memberships;

public class BalanceServiceTests
{
    [Fact]
    public async Task CreatePackage12Async_ShouldCreateMonthlyPackageWithTwelveSessions()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var adminId = Guid.NewGuid();
        var startDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.CreatePackage12Async(
            user.Id,
            new CreatePackage12Request
            {
                StartDate = startDate,
                Notes = "Paket 12"
            },
            adminId);

        response.UserId.Should().Be(user.Id);
        response.PurchaseType.Should().Be(PurchaseType.Package12);
        response.TotalSessions.Should().Be(12);
        response.RemainingSessions.Should().Be(12);
        response.StartDate.Should().Be(startDate);
        response.EndDate.Should().Be(startDate.AddMonths(1));
        response.IsActive.Should().BeTrue();
        response.IsExpired.Should().BeFalse();
        response.Notes.Should().Be("Paket 12");

        var storedBalance = await dbContext.UserTrainingBalances.SingleAsync();
        storedBalance.CreatedByAdminId.Should().Be(adminId);
        storedBalance.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreatePackage6Async_ShouldCreateMonthlyPackageWithSixSessions()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var adminId = Guid.NewGuid();
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.CreatePackage6Async(
            user.Id,
            new CreatePackage6Request
            {
                StartDate = startDate
            },
            adminId);

        response.PurchaseType.Should().Be(PurchaseType.Package6);
        response.TotalSessions.Should().Be(6);
        response.RemainingSessions.Should().Be(6);
        response.EndDate.Should().Be(startDate.AddMonths(1));
    }

    [Fact]
    public async Task CreatePackage12Async_WhenActiveSamePackageExists_ShouldStillCreateNewPackage()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 10,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(25),
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        await dbContext.SaveChangesAsync();

        await balanceService.CreatePackage12Async(
            user.Id,
            new CreatePackage12Request
            {
                StartDate = DateTime.UtcNow
            },
            Guid.NewGuid());

        var packageCount = await dbContext.UserTrainingBalances
            .CountAsync(balance => balance.UserId == user.Id && balance.PurchaseType == PurchaseType.Package12);

        packageCount.Should().Be(2);
    }

    [Fact]
    public async Task AddSingleSessionsAsync_WhenActiveBalanceDoesNotExist_ShouldCreateSingleSessionsBalance()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var adminId = Guid.NewGuid();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.AddSingleSessionsAsync(
            user.Id,
            new AddSingleSessionsRequest
            {
                NumberOfSessions = 3,
                Notes = "Tri pojedinačna termina"
            },
            adminId);

        response.PurchaseType.Should().Be(PurchaseType.SingleSessions);
        response.TotalSessions.Should().Be(3);
        response.RemainingSessions.Should().Be(3);
        response.EndDate.Should().BeNull();
        response.IsActive.Should().BeTrue();
        response.IsExpired.Should().BeFalse();
        response.Notes.Should().Be("Tri pojedinačna termina");

        var storedBalance = await dbContext.UserTrainingBalances.SingleAsync();
        storedBalance.CreatedByAdminId.Should().Be(adminId);
        storedBalance.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task AddSingleSessionsAsync_WhenActiveBalanceExists_ShouldIncreaseTotalsAndUpdateNotes()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.SingleSessions,
            TotalSessions = 2,
            RemainingSessions = 1,
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Notes = "Stara napomena"
        });
        await dbContext.SaveChangesAsync();

        var response = await balanceService.AddSingleSessionsAsync(
            user.Id,
            new AddSingleSessionsRequest
            {
                NumberOfSessions = 4,
                Notes = "Nova napomena"
            },
            Guid.NewGuid());

        response.TotalSessions.Should().Be(6);
        response.RemainingSessions.Should().Be(5);
        response.Notes.Should().Be("Nova napomena");

        var balanceCount = await dbContext.UserTrainingBalances.CountAsync();
        balanceCount.Should().Be(1);
    }

    [Fact]
    public async Task AddSingleSessionsAsync_WhenNumberOfSessionsIsNotPositive_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var user = CreateUser();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var act = () => balanceService.AddSingleSessionsAsync(
            user.Id,
            new AddSingleSessionsRequest
            {
                NumberOfSessions = 0
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Broj termina mora biti veći od 0.");
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
}
