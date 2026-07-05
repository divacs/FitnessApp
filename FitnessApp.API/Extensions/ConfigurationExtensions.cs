using FitnessApp.Application.Settings;

namespace FitnessApp.API.Extensions;

public static class ConfigurationExtensions
{
    private static readonly char[] OriginSeparators = [',', ';'];

    public static IServiceCollection AddApplicationSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appSettingsSection = configuration.GetSection(AppSettings.SectionName);

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JwtSettings:Issuer is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JwtSettings:Audience is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret), "JwtSettings:Secret is required.")
            .Validate(settings => settings.Secret.Trim().Length >= 32, "JwtSettings:Secret must be at least 32 characters long.")
            .Validate(settings => settings.ExpirationMinutes > 0, "JwtSettings:ExpirationMinutes must be greater than zero.")
            .Validate(settings => settings.RefreshTokenExpirationDays > 0, "JwtSettings:RefreshTokenExpirationDays must be greater than zero.")
            .ValidateOnStart();

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
            .Bind(appSettingsSection)
            .Validate(
                _ => ResolveAllowedOrigins(appSettingsSection).Length > 0,
                "AppSettings:AllowedOrigins or AppSettings:FrontendUrl must contain at least one origin.")
            .Validate(
                settings => ResolveAllowedOrigins(appSettingsSection).All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
                "AppSettings:AllowedOrigins or AppSettings:FrontendUrl must contain valid absolute URL values.")
            .Validate(settings => settings.CancellationDeadlineHours >= 0, "AppSettings:CancellationDeadlineHours cannot be negative.")
            .Validate(settings => settings.DefaultTrainingCapacity > 0, "AppSettings:DefaultTrainingCapacity must be greater than zero.")
            .Validate(settings => settings.AutoMarkAttendanceDelayMinutes >= 0, "AppSettings:AutoMarkAttendanceDelayMinutes cannot be negative.");

        return services;
    }

    private static string[] ResolveAllowedOrigins(IConfiguration appSettingsSection)
    {
        var allowedOriginsSection = appSettingsSection.GetSection(nameof(AppSettings.AllowedOrigins));

        var configuredOrigins = allowedOriginsSection.GetChildren()
            .Select(section => section.Value)
            .OfType<string>()
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .SelectMany(origin => origin.Split(OriginSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredOrigins.Length == 0 && !string.IsNullOrWhiteSpace(allowedOriginsSection.Value))
        {
            configuredOrigins = allowedOriginsSection.Value!
                .Split(OriginSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (configuredOrigins.Length > 0)
        {
            return configuredOrigins;
        }

        var frontendUrl = appSettingsSection[nameof(AppSettings.FrontendUrl)];

        return string.IsNullOrWhiteSpace(frontendUrl)
            ? []
            : frontendUrl
                .Split(OriginSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
