using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Auth.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitnessApp.Tests.Auth;

public class AuthServiceTests
{
    private const string Password = "Password123";

    [Fact]
    public async Task LoginAsync_WhenUserIsVerified_ShouldCreateRefreshToken()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var dbContext = services.GetRequiredService<AppDbContext>();

        var response = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        response.UserId.Should().Be(user.Id);

        var storedToken = await dbContext.RefreshTokens.SingleAsync();
        storedToken.Token.Should().Be(response.RefreshToken);
        storedToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsActive_ShouldRotateRefreshToken()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        var refreshResponse = await authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        refreshResponse.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshResponse.RefreshToken.Should().NotBe(loginResponse.RefreshToken);

        var oldToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == loginResponse.RefreshToken);
        oldToken.IsRevoked.Should().BeTrue();
        oldToken.ReplacedByToken.Should().Be(refreshResponse.RefreshToken);

        var newToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == refreshResponse.RefreshToken);
        newToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsRevoked_ShouldRejectReuse()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        await authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        var act = () => authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Refresh token je opozvan.");
    }

    [Theory]
    [InlineData(UserStatus.Blocked)]
    [InlineData(UserStatus.Unverified)]
    public async Task RefreshTokenAsync_WhenUserCannotAuthenticate_ShouldThrowForbidden(UserStatus userStatus)
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        user.UserStatus = userStatus;
        await userManager.UpdateAsync(user);

        var act = () => authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(Options.Create(new JwtSettings
        {
            Issuer = "FitnessApp.Tests",
            Audience = "FitnessApp.Tests",
            Secret = "test-secret-that-is-long-enough-for-hmac-signing",
            ExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        }));

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        UserStatus userStatus)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await roleManager.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.User));

        var email = $"user-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+381600000000",
            UserStatus = userStatus,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(x => x.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, ApplicationRoles.User);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(x => x.Description)));

        return user;
    }
}
