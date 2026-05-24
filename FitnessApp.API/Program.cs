using FitnessApp.API.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services
    .AddApiServices(builder.Configuration)
    .AddSwaggerDocumentation()
    .AddCorsPolicy(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

app.Run();

public partial class Program;
