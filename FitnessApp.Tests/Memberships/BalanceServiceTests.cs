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
    public async Task CreatePackage12Async_WhenPreviousPackage12HasRemainingSessions_ShouldCarryOverMaximumTwoSessions()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var previousPackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 5,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            IsExpired = false,
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(previousPackage);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.CreatePackage12Async(
            user.Id,
            new CreatePackage12Request
            {
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            Guid.NewGuid());

        response.TotalSessions.Should().Be(14);
        response.RemainingSessions.Should().Be(14);
        response.CarriedOverSessions.Should().Be(2);

        var updatedPreviousPackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == previousPackage.Id);
        updatedPreviousPackage.IsActive.Should().BeFalse();
        updatedPreviousPackage.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyCarryOverAsync_ShouldUseOnlyImmediatePreviousPackage12()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var olderPackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 2,
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            IsExpired = false,
            CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var immediatePreviousPackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 1,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            IsExpired = false,
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(olderPackage, immediatePreviousPackage);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.CreatePackage12Async(
            user.Id,
            new CreatePackage12Request
            {
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            Guid.NewGuid());

        response.TotalSessions.Should().Be(13);
        response.RemainingSessions.Should().Be(13);
        response.CarriedOverSessions.Should().Be(1);

        var updatedOlderPackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == olderPackage.Id);
        var updatedImmediatePreviousPackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == immediatePreviousPackage.Id);
        updatedOlderPackage.IsActive.Should().BeTrue();
        updatedOlderPackage.IsExpired.Should().BeFalse();
        updatedImmediatePreviousPackage.IsActive.Should().BeFalse();
        updatedImmediatePreviousPackage.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyCarryOverAsync_WhenCalledTwice_ShouldNotDuplicateCarriedSessions()
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
            RemainingSessions = 2,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            IsExpired = false,
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        var response = await balanceService.CreatePackage12Async(
            user.Id,
            new CreatePackage12Request
            {
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            Guid.NewGuid());

        await balanceService.ApplyCarryOverAsync(user.Id);

        var newPackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == response.Id);
        newPackage.TotalSessions.Should().Be(14);
        newPackage.RemainingSessions.Should().Be(14);
        newPackage.CarriedOverSessions.Should().Be(2);
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

    [Fact]
    public async Task GetCurrentBalanceAsync_WhenUserHasNoBalances_ShouldReturnEmptyBalance()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await balanceService.GetCurrentBalanceAsync(user.Id);

        response.ActivePackage.Should().BeNull();
        response.SingleSessionsRemaining.Should().Be(0);
        response.TotalRemainingSessions.Should().Be(0);
        response.HasAvailableSessions.Should().BeFalse();
        response.MembershipExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ShouldReturnActivePackageAndSingleSessions()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var packageEndDate = DateTime.UtcNow.AddDays(20);
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.Package12,
                TotalSessions = 12,
                RemainingSessions = 7,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = packageEndDate,
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.SingleSessions,
                TotalSessions = 3,
                RemainingSessions = 2,
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = null,
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
        await dbContext.SaveChangesAsync();

        var response = await balanceService.GetCurrentBalanceAsync(user.Id);

        response.ActivePackage.Should().NotBeNull();
        response.ActivePackage!.PurchaseType.Should().Be(PurchaseType.Package12);
        response.ActivePackage.RemainingSessions.Should().Be(7);
        response.SingleSessionsRemaining.Should().Be(2);
        response.TotalRemainingSessions.Should().Be(9);
        response.HasAvailableSessions.Should().BeTrue();
        response.MembershipExpiresAt.Should().Be(packageEndDate);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ShouldIgnoreExpiredInactiveAndEmptyBalances()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.Package12,
                TotalSessions = 12,
                RemainingSessions = 5,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.Package6,
                TotalSessions = 6,
                RemainingSessions = 0,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow
            },
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.SingleSessions,
                TotalSessions = 4,
                RemainingSessions = 4,
                StartDate = DateTime.UtcNow,
                EndDate = null,
                IsActive = false,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow
            },
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.SingleSessions,
                TotalSessions = 2,
                RemainingSessions = 0,
                StartDate = DateTime.UtcNow,
                EndDate = null,
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var response = await balanceService.GetCurrentBalanceAsync(user.Id);

        response.ActivePackage.Should().BeNull();
        response.SingleSessionsRemaining.Should().Be(0);
        response.TotalRemainingSessions.Should().Be(0);
        response.HasAvailableSessions.Should().BeFalse();
        response.MembershipExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeSessionAsync_WhenActivePackageExists_ShouldConsumeFromPackageFirst()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        var packageBalance = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package6,
            TotalSessions = 6,
            RemainingSessions = 2,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(20),
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var singleSessionsBalance = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.SingleSessions,
            TotalSessions = 3,
            RemainingSessions = 3,
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(packageBalance, singleSessionsBalance);
        await dbContext.SaveChangesAsync();

        await balanceService.ConsumeSessionAsync(user.Id);

        var updatedPackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == packageBalance.Id);
        var updatedSingleSessions = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == singleSessionsBalance.Id);
        updatedPackage.RemainingSessions.Should().Be(1);
        updatedSingleSessions.RemainingSessions.Should().Be(3);
    }

    [Fact]
    public async Task ConsumeSessionAsync_WhenNoActivePackageExists_ShouldConsumeFromSingleSessions()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var balanceService = services.GetRequiredService<IBalanceService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.AddRange(
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.Package12,
                TotalSessions = 12,
                RemainingSessions = 5,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.SingleSessions,
                TotalSessions = 4,
                RemainingSessions = 2,
                StartDate = DateTime.UtcNow.AddDays(-3),
                EndDate = null,
                IsActive = true,
                IsExpired = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });
        await dbContext.SaveChangesAsync();

        await balanceService.ConsumeSessionAsync(user.Id);

        var singleSessionsBalance = await dbContext.UserTrainingBalances
            .SingleAsync(x => x.PurchaseType == PurchaseType.SingleSessions);
        singleSessionsBalance.RemainingSessions.Should().Be(1);
    }

    [Fact]
    public async Task ConsumeSessionAsync_WhenBalanceReachesZero_ShouldKeepBalanceActive()
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
            TotalSessions = 1,
            RemainingSessions = 1,
            StartDate = DateTime.UtcNow,
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await balanceService.ConsumeSessionAsync(user.Id);

        var balance = await dbContext.UserTrainingBalances.SingleAsync();
        balance.RemainingSessions.Should().Be(0);
        balance.IsActive.Should().BeTrue();
        balance.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeSessionAsync_WhenNoSessionsAreAvailable_ShouldThrowConflict()
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
            RemainingSessions = 0,
            StartDate = DateTime.UtcNow,
            EndDate = null,
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var act = () => balanceService.ConsumeSessionAsync(user.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Korisnik nema dostupnih termina.");
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
