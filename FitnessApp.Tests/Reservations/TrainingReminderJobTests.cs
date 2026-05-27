using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Jobs;
using FitnessApp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Reservations;

public class TrainingReminderJobTests
{
    [Fact]
    public async Task ExecuteAsync_WhenReservedTrainingStartsIn24Hours_ShouldSendReminderAndSetTimestamp()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<TrainingReminderJob>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(24).AddMinutes(5));
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.SentEmails.Should().ContainSingle();

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.ReminderSentAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Attended)]
    [InlineData(ReservationStatus.NoShow)]
    public async Task ExecuteAsync_WhenReservationIsNotReserved_ShouldNotSendReminder(ReservationStatus status)
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<TrainingReminderJob>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(24).AddMinutes(5));
        var reservation = CreateReservation(user.Id, training.Id);
        reservation.Status = status;
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.SentEmails.Should().BeEmpty();

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.ReminderSentAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReminderWasAlreadySent_ShouldNotSendReminderAgain()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<TrainingReminderJob>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(24).AddMinutes(5));
        var reservation = CreateReservation(user.Id, training.Id);
        reservation.ReminderSentAt = DateTime.UtcNow.AddHours(-1);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrainingIsOutsideReminderWindow_ShouldNotSendReminder()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var emailService = services.GetRequiredService<FakeEmailService>();
        var job = services.GetRequiredService<TrainingReminderJob>();
        var user = CreateUser();
        var training = CreateTraining(DateTime.UtcNow.AddHours(25));
        var reservation = CreateReservation(user.Id, training.Id);
        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await job.ExecuteAsync();

        emailService.SentEmails.Should().BeEmpty();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<FakeEmailService>();
        services.AddScoped<IEmailService>(provider => provider.GetRequiredService<FakeEmailService>());
        services.AddScoped<TrainingReminderJob>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+381600000000",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TrainingSession CreateTraining(DateTime startTime)
    {
        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Trening",
            Description = string.Empty,
            StartTime = startTime,
            EndTime = startTime.AddHours(1),
            Capacity = 10,
            TrainerName = "Sara",
            Location = "Studio",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Reservation CreateReservation(Guid userId, Guid trainingSessionId)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrainingSessionId = trainingSessionId,
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<string> SentEmails { get; } = new();

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string plainTextBody,
            CancellationToken cancellationToken = default)
        {
            SentEmails.Add(toEmail);
            return Task.CompletedTask;
        }

        public Task SendRegistrationPendingApprovalEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendUserVerifiedEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendMembershipExpiringEmailAsync(
            string toEmail,
            string firstName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
