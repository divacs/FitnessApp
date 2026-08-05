using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Tests.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Payments;

public class PaymentsApiTests
{
    private const string Password = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetPayments_AsAdmin_ShouldReturnPaginatedPayments()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var admin = await CreateAdminAsync(factory);
        var paymentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "payment-user@example.com",
            Email = "payment-user@example.com",
            FirstName = "Nenad",
            LastName = "Lazic",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Users.Add(paymentUser);
            dbContext.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = paymentUser.Id,
                Amount = 4500,
                PaymentDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                PaymentType = PurchaseType.Package12,
                NumberOfSessions = 12,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = admin.Id
            });
            await dbContext.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = admin.Email!,
            Password = Password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);
        loginPayload!.Data.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginPayload.Data!.AccessToken);

        var response = await client.GetAsync("/api/admin/payments?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResponse<PaymentResponse>>>(JsonOptions);
        payload!.Data.Should().NotBeNull();
        payload.Data!.TotalCount.Should().Be(1);
        payload.Data.Items.Should().ContainSingle();
        payload.Data.Items.Single().UserFullName.Should().Be("Nenad Lazic");
    }

    private static async Task<ApplicationUser> CreateAdminAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(RoleConstants.Admin))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(RoleConstants.Admin));
            createRoleResult.Succeeded.Should().BeTrue();
        }

        var email = $"admin-payments-{Guid.NewGuid():N}@example.com";
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Admin",
            LastName = "Payments",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            VerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(admin, Password);
        createResult.Succeeded.Should().BeTrue();

        var addRoleResult = await userManager.AddToRoleAsync(admin, RoleConstants.Admin);
        addRoleResult.Succeeded.Should().BeTrue();

        return admin;
    }
}
