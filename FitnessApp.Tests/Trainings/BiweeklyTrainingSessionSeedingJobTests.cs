using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Application.Features.Settings.DTOs;
using FitnessApp.Application.Features.Settings.Interfaces;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Trainings;

public class BiweeklyTrainingSessionSeedingJobTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCreateOnlyMissingTrainingSessionsWithinNextFourteenDays()
    {
        var timeZone = BiweeklyTrainingSessionSeedingJob.ResolveTimeZone();
        var utcNow = new DateTimeOffset(2026, 7, 31, 6, 0, 0, TimeSpan.Zero);
        var existingLocalStartTime = new DateTime(2026, 8, 2, 10, 0, 0);
        var existingUtcStartTime = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(existingLocalStartTime, DateTimeKind.Unspecified),
            timeZone);

        var services = CreateServiceProvider(utcNow);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var job = services.GetRequiredService<BiweeklyTrainingSessionSeedingJob>();

        dbContext.TrainingSessions.Add(new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Old Template Title",
            Description = "Grupni trening",
            StartTime = existingUtcStartTime,
            EndTime = existingUtcStartTime.AddMinutes(75),
            Capacity = 14,
            TrainerName = "Sara",
            Location = "Old Studio",
            CreatedAt = utcNow.UtcDateTime.AddDays(-1),
            UpdatedAt = utcNow.UtcDateTime.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        var trainings = await dbContext.TrainingSessions
            .OrderBy(training => training.StartTime)
            .ToListAsync();

        trainings.Should().HaveCount(6);
        trainings.Count(training => training.StartTime == existingUtcStartTime).Should().Be(1);
        trainings.Count(training => training.Title == "Full Body Fitness").Should().Be(5);
        trainings.Count(training => training.Location == "Srnetička 4").Should().Be(5);
        trainings.Should().OnlyContain(training => training.Description == "Grupni trening");
        trainings.Should().OnlyContain(training => training.Capacity == 14);
        trainings.Should().OnlyContain(training => training.TrainerName == "Sara");
        trainings.Should().OnlyContain(training => training.EndTime - training.StartTime == TimeSpan.FromMinutes(75));
        trainings.Single(training => training.StartTime == existingUtcStartTime).Title.Should().Be("Old Template Title");
        trainings.Single(training => training.StartTime == existingUtcStartTime).Location.Should().Be("Old Studio");
    }

    [Fact]
    public async Task ExecuteAsync_WhenWeekIsOutsideBiweeklyCadence_ShouldSkipSeeding()
    {
        var utcNow = new DateTimeOffset(2026, 8, 7, 6, 0, 0, TimeSpan.Zero);
        var services = CreateServiceProvider(utcNow);
        var dbContext = services.GetRequiredService<AppDbContext>();
        var job = services.GetRequiredService<BiweeklyTrainingSessionSeedingJob>();

        await job.ExecuteAsync();

        var trainingCount = await dbContext.TrainingSessions.CountAsync();
        trainingCount.Should().Be(0);
    }

    private static ServiceProvider CreateServiceProvider(DateTimeOffset utcNow, int defaultTrainingCapacity = 12)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<FakeNotificationService>();
        services.AddScoped<INotificationService>(provider => provider.GetRequiredService<FakeNotificationService>());
        services.AddSingleton<ISettingsService>(new FakeSettingsService(defaultTrainingCapacity));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(utcNow));
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<BiweeklyTrainingSessionSeedingJob>();

        return services.BuildServiceProvider();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<NotificationResponse> CreateNotificationAsync(
            CreateNotificationRequest request,
            Guid adminId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NotificationResponse());
        }

        public Task<NotificationResponse> SendGlobalNotificationAsync(
            CreateNotificationRequest request,
            Guid adminId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NotificationResponse());
        }

        public Task<PaginatedResponse<NotificationResponse>> GetMyNotificationsAsync(
            Guid userId,
            int page,
            int pageSize,
            bool unreadOnly = false,
            NotificationType? type = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaginatedResponse<NotificationResponse>(
                Array.Empty<NotificationResponse>(),
                page,
                pageSize,
                0));
        }

        public Task MarkAsReadAsync(
            Guid userId,
            Guid userNotificationId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<PaginatedResponse<NotificationResponse>> GetNotificationsAsync(
            int page,
            int pageSize,
            NotificationType? type = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaginatedResponse<NotificationResponse>(
                Array.Empty<NotificationResponse>(),
                page,
                pageSize,
                0));
        }

        public Task SendTrainingCancelledNotificationsAsync(
            Guid trainingSessionId,
            string cancellationReason,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendTrainingUpdatedNotificationsAsync(
            Guid trainingSessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly int _defaultTrainingCapacity;

        public FakeSettingsService(int defaultTrainingCapacity)
        {
            _defaultTrainingCapacity = defaultTrainingCapacity;
        }

        public Task<SettingsResponse> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SettingsResponse
            {
                CancellationDeadlineHours = 12,
                ContactPhone = "+381600000000",
                DefaultTrainingCapacity = _defaultTrainingCapacity,
                AutoMarkAttendanceDelayMinutes = 60
            });
        }

        public Task<SettingsResponse> UpdateSettingsAsync(
            UpdateSettingsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SettingsResponse());
        }

        public Task<int> GetCancellationDeadlineHoursAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(12);
        }

        public Task<int> GetDefaultTrainingCapacityAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_defaultTrainingCapacity);
        }

        public Task<int> GetAutoMarkAttendanceDelayMinutesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(60);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
