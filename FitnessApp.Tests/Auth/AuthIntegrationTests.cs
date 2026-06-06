using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Auth;

public class AuthIntegrationTests
{
    private const string Password = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Register_ShouldCreateUnverifiedUser()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var request = new RegisterRequest
        {
            FirstName = "Sara",
            LastName = "Test",
            Email = $"register-{Guid.NewGuid():N}@example.com",
            Password = Password,
            PhoneNumber = "+381600000001"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (message, data) = await ReadSuccessAsync<CurrentUserResponse>(response);
        message.Should().Be("Registracija je uspešna. Sačekajte verifikaciju naloga.");
        data.Email.Should().Be(request.Email);
        data.FirstName.Should().Be(request.FirstName);
        data.LastName.Should().Be(request.LastName);
        data.UserStatus.Should().Be(UserStatus.Unverified);
        data.Role.Should().Be(RoleConstants.User);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(request.Email);

        user.Should().NotBeNull();
        user!.UserStatus.Should().Be(UserStatus.Unverified);
    }

    [Theory]
    [InlineData(UserStatus.Unverified, "Korisnik još nije verifikovan.")]
    [InlineData(UserStatus.Blocked, "Korisnik je blokiran.")]
    public async Task Login_ShouldRejectUserThatCannotAuthenticate(
        UserStatus userStatus,
        string expectedMessage)
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(factory, userStatus);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await ReadErrorAsync(response);
        error.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task Login_ShouldSucceedForVerifiedUser()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(factory, UserStatus.Verified);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (message, data) = await ReadSuccessAsync<AuthResponse>(response);
        message.Should().Be("OK");
        data.UserId.Should().Be(user.Id);
        data.Email.Should().Be(user.Email);
        data.UserStatus.Should().Be(UserStatus.Verified);
        data.AccessToken.Should().NotBeNullOrWhiteSpace();
        data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnNewAccessAndRefreshTokens()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(factory, UserStatus.Verified);
        var loginPayload = await LoginAsync(client, user.Email!);

        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = loginPayload.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, data) = await ReadSuccessAsync<AuthResponse>(response);
        data.UserId.Should().Be(user.Id);
        data.AccessToken.Should().NotBe(loginPayload.AccessToken);
        data.RefreshToken.Should().NotBe(loginPayload.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ShouldRotateTokenAndRejectReuse()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(factory, UserStatus.Verified);
        var loginPayload = await LoginAsync(client, user.Email!);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = loginPayload.RefreshToken
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var (_, rotatedData) = await ReadSuccessAsync<AuthResponse>(refreshResponse);

        var reusedResponse = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = loginPayload.RefreshToken
        });

        reusedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await ReadErrorAsync(reusedResponse);
        error.Message.Should().Be("Refresh token je već iskorišćen ili opozvan.");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == loginPayload.RefreshToken);
        var replacementToken = await dbContext.RefreshTokens.SingleAsync(x => x.Token == rotatedData.RefreshToken);

        oldToken.IsRevoked.Should().BeTrue();
        oldToken.ReplacedByToken.Should().Be(rotatedData.RefreshToken);
        replacementToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshToken()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(factory, UserStatus.Verified);
        var loginPayload = await LoginAsync(client, user.Email!);

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new RevokeTokenRequest
        {
            RefreshToken = loginPayload.RefreshToken
        });

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var logoutMessage = await ReadSuccessMessageAsync(logoutResponse);
        logoutMessage.Should().Be("Uspešno ste se odjavili.");

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = loginPayload.RefreshToken
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await ReadErrorAsync(refreshResponse);
        error.Message.Should().Be("Refresh token je već iskorišćen ili opozvan.");
    }

    private static async Task<ApplicationUser> CreateUserAsync(AuthApiFactory factory, UserStatus userStatus)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(RoleConstants.User))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(RoleConstants.User));
            createRoleResult.Succeeded.Should().BeTrue();
        }

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
            CreatedAt = DateTime.UtcNow,
            VerifiedAt = userStatus == UserStatus.Verified ? DateTime.UtcNow : null,
            BlockedAt = userStatus == UserStatus.Blocked ? DateTime.UtcNow : null
        };

        var createResult = await userManager.CreateAsync(user, Password);
        createResult.Succeeded.Should().BeTrue();

        var roleResult = await userManager.AddToRoleAsync(user, RoleConstants.User);
        roleResult.Succeeded.Should().BeTrue();

        return user;
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, data) = await ReadSuccessAsync<AuthResponse>(response);
        return data;
    }

    private static async Task<(string Message, T Data)> ReadSuccessAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Data.Should().NotBeNull();
        return (payload.Message, payload.Data!);
    }

    private static async Task<string> ReadSuccessMessageAsync(HttpResponseMessage response)
    {
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(contentStream);

        return document.RootElement.GetProperty("message").GetString()!;
    }

    private static async Task<ErrorResponse> ReadErrorAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        payload.Should().NotBeNull();
        return payload!;
    }
}
