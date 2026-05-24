using FitnessApp.Application.Settings;

namespace FitnessApp.API.Extensions;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddApplicationSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JwtSettings:Issuer is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JwtSettings:Audience is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret), "JwtSettings:Secret is required.")
            .Validate(settings => settings.ExpirationMinutes > 0, "JwtSettings:ExpirationMinutes must be greater than zero.");

        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.SmtpHost), "EmailSettings:SmtpHost is required.")
            .Validate(settings => settings.SmtpPort > 0, "EmailSettings:SmtpPort must be greater than zero.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromEmail), "EmailSettings:FromEmail is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromName), "EmailSettings:FromName is required.");

        services.AddOptions<HangfireSettings>()
            .Bind(configuration.GetSection(HangfireSettings.SectionName))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.DashboardPath) && settings.DashboardPath.StartsWith('/'),
                "HangfireSettings:DashboardPath must start with '/'.");

        services.AddOptions<AdminSeedSettings>()
            .Bind(configuration.GetSection(AdminSeedSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Email), "AdminSeed:Email is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Password), "AdminSeed:Password is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FirstName), "AdminSeed:FirstName is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.LastName), "AdminSeed:LastName is required.");

        services.AddOptions<AppSettings>()
            .Bind(configuration.GetSection(AppSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FrontendUrl), "AppSettings:FrontendUrl is required.")
            .Validate(settings => Uri.TryCreate(settings.FrontendUrl, UriKind.Absolute, out _), "AppSettings:FrontendUrl must be a valid absolute URL.")
            .Validate(settings => settings.CancellationDeadlineHours >= 0, "AppSettings:CancellationDeadlineHours cannot be negative.")
            .Validate(settings => settings.DefaultTrainingCapacity > 0, "AppSettings:DefaultTrainingCapacity must be greater than zero.")
            .Validate(settings => settings.AutoMarkAttendanceDelayMinutes >= 0, "AppSettings:AutoMarkAttendanceDelayMinutes cannot be negative.");

        return services;
    }
}
