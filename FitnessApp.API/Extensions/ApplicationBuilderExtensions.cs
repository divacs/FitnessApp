using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
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
}
