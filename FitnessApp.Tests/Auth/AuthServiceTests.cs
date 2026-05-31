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
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
    public async Task LoginAsync_WhenUserIsSoftDeleted_ShouldRejectAuthentication()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var authService = services.GetRequiredService<IAuthService>();

        user.IsDeleted = true;
        await userManager.UpdateAsync(user);

        var act = () => authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Email ili lozinka nisu ispravni.");
    }

    [Fact]
    public async Task LoginAsync_ShouldIssueJwtWithExpectedClaims()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        token.Issuer.Should().Be("FitnessApp.Tests");
        token.Audiences.Should().Contain("FitnessApp.Tests");
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Jti && !string.IsNullOrWhiteSpace(x.Value));
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Sub && x.Value == user.Id.ToString());
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Email && x.Value == user.Email);
        token.Claims.Should().Contain(x => x.Type == ClaimTypes.NameIdentifier && x.Value == user.Id.ToString());
        token.Claims.Should().Contain(x => x.Type == ClaimTypes.Email && x.Value == user.Email);
        token.Claims.Should().Contain(x => x.Type == ClaimTypes.Name && x.Value == user.FullName);
        token.Claims.Should().Contain(x => x.Type == ClaimTypes.Role && x.Value == RoleConstants.User);
        token.Claims.Should().Contain(x => x.Type == AuthClaimConstants.UserStatus && x.Value == UserStatus.Verified.ToString());
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
    public async Task RefreshTokenAsync_WhenTokenIsRevoked_ShouldRejectReuseAndRevokeTokenFamily()
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

        var rotatedResponse = await authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        var act = () => authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Refresh token je već iskorišćen ili opozvan.");

        var replacementToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == rotatedResponse.RefreshToken);
        replacementToken.IsRevoked.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserStatus.Blocked, "Korisnik je blokiran.")]
    [InlineData(UserStatus.Unverified, "Korisnik još nije verifikovan.")]
    public async Task RefreshTokenAsync_WhenUserCannotAuthenticate_ShouldThrowForbiddenAndRevokeActiveTokens(
        UserStatus userStatus,
        string expectedMessage)
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<AppDbContext>();
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

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage(expectedMessage);

        var storedToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == loginResponse.RefreshToken);
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenUserIsSoftDeleted_ShouldThrowForbiddenAndRevokeActiveTokens()
    {
        var services = CreateServiceProvider();
        var user = await CreateUserAsync(services, UserStatus.Verified);
        var authService = services.GetRequiredService<IAuthService>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        user.IsDeleted = true;
        await userManager.UpdateAsync(user);

        var act = () => authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Korisnički nalog više nije dostupan.");

        var storedToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == loginResponse.RefreshToken);
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeTokenAsync_WhenTokenIsActive_ShouldRevokeIt()
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

        await authService.RevokeTokenAsync(new RevokeTokenRequest
        {
            RefreshToken = loginResponse.RefreshToken
        });

        var storedToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == loginResponse.RefreshToken);
        storedToken.IsRevoked.Should().BeTrue();
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

        await roleManager.CreateAsync(new IdentityRole<Guid>(RoleConstants.User));

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

        var roleResult = await userManager.AddToRoleAsync(user, RoleConstants.User);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(x => x.Description)));

        return user;
    }
}
