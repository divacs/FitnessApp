using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Auth.Validators;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace FitnessApp.API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "FrontendCorsPolicy";
    private static readonly char[] OriginSeparators = [',', ';'];

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationSettings(configuration);
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Values
                        .SelectMany(value => value.Errors)
                        .Select(error => error.ErrorMessage)
                        .Where(error => !string.IsNullOrWhiteSpace(error))
                        .ToArray();

                    return new BadRequestObjectResult(new ErrorResponse
                    {
                        Message = "Validacija nije uspela.",
                        Errors = errors
                    });
                };
            });
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddAuthorizationPolicies();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>();

        if (jwtSettings is null)
        {
            throw new InvalidOperationException("JwtSettings configuration is required.");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                        if (!Guid.TryParse(userIdValue, out var userId))
                        {
                            context.Fail("Pristupni token nije validan.");
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var user = await dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);

                        if (user is null || user.IsDeleted)
                        {
                            context.Fail("Korisnički nalog više nije dostupan.");
                            return;
                        }

                        if (user.UserStatus == UserStatus.Blocked)
                        {
                            context.Fail("Korisnik je blokiran.");
                            return;
                        }

                        if (user.UserStatus != UserStatus.Verified)
                        {
                            context.Fail("Korisnik još nije verifikovan.");
                        }
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FitnessApp API",
                Version = "v1",
                Description = "API dokumentacija za FitnessApp frontend i admin integraciju."
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Unesite JWT token u formatu: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [securityScheme] = Array.Empty<string>()
            });

            options.TagActionsBy(apiDescription =>
            {
                var controllerName = apiDescription.ActionDescriptor.RouteValues["controller"];
                return [ResolveSwaggerTag(controllerName)];
            });

            options.OrderActionsBy(apiDescription =>
            {
                var method = apiDescription.HttpMethod ?? string.Empty;
                return $"{ResolveSwaggerTag(apiDescription.ActionDescriptor.RouteValues["controller"])}_{method}_{apiDescription.RelativePath}";
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    private static string ResolveSwaggerTag(string? controllerName)
    {
        return controllerName switch
        {
            "Auth" => "Auth",
            "Reservations" or "AdminReservations" => "Reservations",
            "AdminPayments" => "Payments",
            "UserBalances" or "AdminBalances" => "Memberships & Balances",
            "Notifications" or "AdminNotifications" => "Notifications",
            _ => controllerName?
                .Replace("Admin", string.Empty, StringComparison.Ordinal)
                .Replace("Controller", string.Empty, StringComparison.Ordinal)
                ?? "API"
        };
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appSettingsSection = configuration.GetSection(AppSettings.SectionName);
        var allowedOrigins = ResolveAllowedOrigins(appSettingsSection);

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("AppSettings:AllowedOrigins or AppSettings:FrontendUrl must contain at least one valid origin.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .WithHeaders(
                        HeaderNames.Authorization,
                        HeaderNames.ContentType,
                        HeaderNames.Accept,
                        HeaderNames.Origin,
                        HeaderNames.XRequestedWith)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });

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
