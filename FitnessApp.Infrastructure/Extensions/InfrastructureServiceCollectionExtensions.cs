using FitnessApp.Domain.Entities;
using FitnessApp.Application.Features.Auth.Interfaces;
using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Payments.Interfaces;
using FitnessApp.Application.Features.Reservations.Interfaces;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Emails;
using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddHangfire(configuration =>
        {
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true
                });
        });
        services.AddHangfireServer();

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IIdentitySeeder, IdentitySeeder>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IAutoAttendanceService, AutoAttendanceService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<AutoMarkAttendanceJob>();
        services.AddScoped<TrainingReminderJob>();

        return services;
    }
}
