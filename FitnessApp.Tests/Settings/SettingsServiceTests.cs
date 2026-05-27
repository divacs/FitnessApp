using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Settings.DTOs;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitnessApp.Tests.Settings;

public class SettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_WhenDatabaseSettingsDoNotExist_ShouldReturnFallbackSettings()
    {
        var services = CreateServiceProvider();
        var settingsService = services.GetRequiredService<ISettingsService>();

        var response = await settingsService.GetSettingsAsync();

        response.CancellationDeadlineHours.Should().Be(12);
        response.ContactPhone.Should().Be("+381000000000");
        response.DefaultTrainingCapacity.Should().Be(10);
        response.AutoMarkAttendanceDelayMinutes.Should().Be(60);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpsertSettings()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var settingsService = services.GetRequiredService<ISettingsService>();

        var response = await settingsService.UpdateSettingsAsync(new UpdateSettingsRequest
        {
            CancellationDeadlineHours = 6,
            ContactPhone = " +38160111222 ",
            DefaultTrainingCapacity = 14,
            AutoMarkAttendanceDelayMinutes = 45
        });

        response.CancellationDeadlineHours.Should().Be(6);
        response.ContactPhone.Should().Be("+38160111222");
        response.DefaultTrainingCapacity.Should().Be(14);
        response.AutoMarkAttendanceDelayMinutes.Should().Be(45);

        var storedSettings = await dbContext.AppSettings.ToListAsync();
        storedSettings.Should().HaveCount(4);
        storedSettings.Should().OnlyContain(setting => setting.UpdatedAt != default);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenValuesExist_ShouldUpdateExistingRows()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var settingsService = services.GetRequiredService<ISettingsService>();

        await settingsService.UpdateSettingsAsync(new UpdateSettingsRequest
        {
            CancellationDeadlineHours = 6,
            ContactPhone = "+38160111222",
            DefaultTrainingCapacity = 14,
            AutoMarkAttendanceDelayMinutes = 45
        });

        await settingsService.UpdateSettingsAsync(new UpdateSettingsRequest
        {
            CancellationDeadlineHours = 8,
            ContactPhone = "+38160999888",
            DefaultTrainingCapacity = 16,
            AutoMarkAttendanceDelayMinutes = 30
        });

        var storedSettings = await dbContext.AppSettings.ToListAsync();
        storedSettings.Should().HaveCount(4);

        var response = await settingsService.GetSettingsAsync();
        response.CancellationDeadlineHours.Should().Be(8);
        response.ContactPhone.Should().Be("+38160999888");
        response.DefaultTrainingCapacity.Should().Be(16);
        response.AutoMarkAttendanceDelayMinutes.Should().Be(30);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenValuesAreInvalid_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var settingsService = services.GetRequiredService<ISettingsService>();

        var act = () => settingsService.UpdateSettingsAsync(new UpdateSettingsRequest
        {
            CancellationDeadlineHours = -1,
            ContactPhone = "+38160111222",
            DefaultTrainingCapacity = 10,
            AutoMarkAttendanceDelayMinutes = 60
        });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Rok za otkazivanje ne može biti negativan.");
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new AppSettings
        {
            ContactPhone = "+381000000000",
            FrontendUrl = "http://localhost:5173",
            CancellationDeadlineHours = 12,
            DefaultTrainingCapacity = 10,
            AutoMarkAttendanceDelayMinutes = 60
        }));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<ISettingsService, SettingsService>();

        return services.BuildServiceProvider();
    }
}
