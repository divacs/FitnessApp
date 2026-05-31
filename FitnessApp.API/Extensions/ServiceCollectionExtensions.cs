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
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

namespace FitnessApp.API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "FrontendCorsPolicy";

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
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter JWT Bearer token.",
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
        });

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
