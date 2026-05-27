using FitnessApp.API.Middleware;
using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FitnessApp.API.Extensions;

public static class ApplicationBuilderExtensions
{
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(SeedIdentityAsync));

        if (!await dbContext.Database.CanConnectAsync())
        {
            logger.LogWarning("Skipping identity seed because the database connection is not available.");
            return;
        }

        var identitySeeder = scope.ServiceProvider.GetRequiredService<IIdentitySeeder>();

        await identitySeeder.SeedAsync();
    }

    public static async Task<WebApplication> RegisterRecurringJobsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(RegisterRecurringJobsAsync));

        if (!await dbContext.Database.CanConnectAsync())
        {
            logger.LogWarning("Skipping recurring job registration because the database connection is not available.");
            return app;
        }

        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<AutoMarkAttendanceJob>(
            "auto-mark-attendance",
            job => job.ExecuteAsync(),
            "*/30 * * * *");

        recurringJobManager.AddOrUpdate<TrainingReminderJob>(
            "training-reminders",
            job => job.ExecuteAsync(),
            "*/30 * * * *");

        recurringJobManager.AddOrUpdate<MembershipExpirationReminderJob>(
            "membership-expiration-reminders",
            job => job.ExecuteAsync(),
            "0 9 * * *");

        logger.LogInformation("Registered recurring jobs.");

        return app;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseGlobalExceptionHandling();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Text("Healthy", "text/plain"))
            .WithName("HealthCheck")
            .AllowAnonymous();

        app.MapControllers();

        return app;
    }

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
