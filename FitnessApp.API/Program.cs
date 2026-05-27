using FitnessApp.API.Extensions;
using FitnessApp.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "FitnessApp");
});

builder.Services
    .AddApiServices(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerDocumentation()
    .AddCorsPolicy(builder.Configuration);

var app = builder.Build();

await app.SeedIdentityAsync();
await app.RegisterRecurringJobsAsync();

app.UseApiPipeline();

app.Run();

public partial class Program;
