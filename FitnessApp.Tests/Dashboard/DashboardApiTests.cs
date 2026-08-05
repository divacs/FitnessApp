using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Dashboard.DTOs;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Tests.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Dashboard;

public class DashboardApiTests
{
    private const string Password = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetDashboard_WithActiveData_ShouldReturnOk()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await CreateVerifiedUserAsync(factory);
        var startDate = DateTime.UtcNow.AddDays(-2);
        var training = new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Full Body Fitness",
            Description = string.Empty,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Capacity = 12,
            TrainerName = "Sara",
            Location = "Srneticka 4"
        };

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 4500,
                PaymentDate = startDate,
                StartDate = startDate,
                PaymentType = PurchaseType.Package12,
                NumberOfSessions = 12
            };

            dbContext.UserTrainingBalances.Add(new UserTrainingBalance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PurchaseType = PurchaseType.Package12,
                TotalSessions = 12,
                RemainingSessions = 6,
                StartDate = startDate,
                EndDate = DateTime.UtcNow.AddDays(28),
                IsActive = true,
                IsExpired = false
            });
            dbContext.TrainingSessions.Add(training);
            dbContext.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TrainingSessionId = training.Id,
                Status = ReservationStatus.Reserved
            });
            dbContext.Payments.Add(payment);
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                Title = "Podsetnik",
                Message = "Vidimo se na treningu.",
                Type = NotificationType.General,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Notifications.Add(notification);
            dbContext.UserNotifications.Add(new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                NotificationId = notification.Id,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = user.Email!,
            Password = Password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);
        loginPayload!.Data.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginPayload.Data!.AccessToken);

        var response = await client.GetAsync("/api/me/dashboard");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            string.Join("; ", response.Headers.WwwAuthenticate.Select(header => header.ToString())));
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UserDashboardResponse>>(JsonOptions);
        payload!.Data.Should().NotBeNull();
        payload.Data!.ActiveMembership!.PaymentId.Should().NotBeNull();
        payload.Data.UpcomingReservations.Should().ContainSingle();
        payload.Data.LatestNotifications.Should().ContainSingle();
        payload.Data.LatestNotifications.Single().Title.Should().Be("Podsetnik");
    }

    private static async Task<ApplicationUser> CreateVerifiedUserAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(RoleConstants.User))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(RoleConstants.User));
            createRoleResult.Succeeded.Should().BeTrue();
        }

        var email = $"dashboard-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Dashboard",
            LastName = "Test",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            VerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        createResult.Succeeded.Should().BeTrue();

        var roleResult = await userManager.AddToRoleAsync(user, RoleConstants.User);
        roleResult.Succeeded.Should().BeTrue();

        return user;
    }
}
