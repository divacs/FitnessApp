using FitnessApp.Application.Settings;

namespace FitnessApp.API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "FrontendCorsPolicy";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationSettings(configuration);
        services.AddControllers();
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var frontendUrl = configuration
            .GetSection(AppSettings.SectionName)
            .Get<AppSettings>()?
            .FrontendUrl;

        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            throw new InvalidOperationException("AppSettings:FrontendUrl is required for CORS configuration.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                policy
                    .WithOrigins(frontendUrl)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
