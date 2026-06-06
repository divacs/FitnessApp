using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace FitnessApp.Tests.Auth;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"auth-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=FitnessAppAuthTests;Trusted_Connection=True;",
                ["JwtSettings:Issuer"] = "FitnessApp.IntegrationTests",
                ["JwtSettings:Audience"] = "FitnessApp.IntegrationTests",
                ["JwtSettings:Secret"] = "integration-test-secret-that-is-long-enough",
                ["JwtSettings:ExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["AppSettings:FrontendUrl"] = "http://localhost:5173",
                ["AdminSeed:Email"] = "admin@test.local",
                ["AdminSeed:Password"] = "Admin1234",
                ["AdminSeed:FirstName"] = "Admin",
                ["AdminSeed:LastName"] = "Test"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IRecurringJobManager>();
            services.RemoveAll<IBackgroundJobClient>();
            services.RemoveAll<IEmailService>();

            var hangfireHostedServices = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("Hangfire", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var hostedService in hangfireHostedServices)
            {
                services.Remove(hostedService);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.AddSingleton<IRecurringJobManager, NoOpRecurringJobManager>();
            services.AddSingleton<IBackgroundJobClient, NoOpBackgroundJobClient>();
            services.AddScoped<IEmailService, NoOpEmailService>();
        });
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string plainTextBody,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendRegistrationPendingApprovalEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendUserVerifiedEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendMembershipExpiringEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpRecurringJobManager : IRecurringJobManager
    {
        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
        {
        }

        public void Trigger(string recurringJobId)
        {
        }

        public void RemoveIfExists(string recurringJobId)
        {
        }
    }

    private sealed class NoOpBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString("N");

        public bool ChangeState(string jobId, IState state, string expectedState) => true;

        public bool Delete(string jobId) => true;

        public bool Delete(string jobId, string fromState) => true;

        public bool Requeue(string jobId) => true;

        public bool Requeue(string jobId, string fromState) => true;
    }
}
