using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Trainings;

public class TrainingServiceTests
{
    [Fact]
    public async Task GetUpcomingTrainingsAsync_ShouldReturnOnlyFutureTrainingsSortedByStartTime()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var laterTraining = CreateTraining(DateTime.UtcNow.AddDays(2), "Kasniji trening");
        var earlierTraining = CreateTraining(DateTime.UtcNow.AddDays(1), "Raniji trening");
        var pastTraining = CreateTraining(DateTime.UtcNow.AddDays(-1), "Prosli trening");
        dbContext.TrainingSessions.AddRange(laterTraining, earlierTraining, pastTraining);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.GetUpcomingTrainingsAsync();

        response.Should().HaveCount(2);
        response.Select(training => training.Id).Should().Equal(earlierTraining.Id, laterTraining.Id);
    }

    [Fact]
    public async Task GetUpcomingTrainingsAsync_ShouldExcludeCancelledTrainingsByDefault()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var activeTraining = CreateTraining(DateTime.UtcNow.AddDays(1), "Aktivan trening");
        var cancelledTraining = CreateTraining(DateTime.UtcNow.AddDays(2), "Otkazan trening");
        cancelledTraining.IsCancelled = true;
        dbContext.TrainingSessions.AddRange(activeTraining, cancelledTraining);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.GetUpcomingTrainingsAsync();

        response.Should().ContainSingle();
        response.Single().Id.Should().Be(activeTraining.Id);
    }

    [Fact]
    public async Task GetUpcomingTrainingsAsync_WhenCancelledFilterIsTrue_ShouldReturnCancelledTrainings()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var activeTraining = CreateTraining(DateTime.UtcNow.AddDays(1), "Aktivan trening");
        var cancelledTraining = CreateTraining(DateTime.UtcNow.AddDays(2), "Otkazan trening");
        cancelledTraining.IsCancelled = true;
        dbContext.TrainingSessions.AddRange(activeTraining, cancelledTraining);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.GetUpcomingTrainingsAsync(isCancelled: true);

        response.Should().ContainSingle();
        response.Single().Id.Should().Be(cancelledTraining.Id);
    }

    [Fact]
    public async Task GetUpcomingTrainingsAsync_WhenDateFilterIsApplied_ShouldReturnTrainingsForThatDate()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var requestedDate = DateTime.UtcNow.AddDays(3).Date;
        var matchingTraining = CreateTraining(requestedDate.AddHours(10), "Trazeni trening");
        var otherTraining = CreateTraining(requestedDate.AddDays(1).AddHours(10), "Drugi trening");
        dbContext.TrainingSessions.AddRange(matchingTraining, otherTraining);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.GetUpcomingTrainingsAsync(date: requestedDate);

        response.Should().ContainSingle();
        response.Single().Id.Should().Be(matchingTraining.Id);
    }

    [Fact]
    public async Task GetTrainingByIdAsync_ShouldReturnTrainingWithReservedCount()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Pilates");
        training.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Reserved
        });
        training.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Cancelled
        });
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.GetTrainingByIdAsync(training.Id);

        response.Id.Should().Be(training.Id);
        response.ReservedCount.Should().Be(1);
        response.AvailableSpots.Should().Be(training.Capacity - 1);
    }

    [Fact]
    public async Task CreateTrainingAsync_ShouldCreateTrainingWithDefaults()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var startTime = DateTime.UtcNow.AddDays(1);

        var response = await trainingService.CreateTrainingAsync(
            new CreateTrainingSessionRequest
            {
                Title = "  Jutarnji trening  ",
                StartTime = startTime,
                EndTime = startTime.AddHours(1),
                Capacity = 12
            });

        response.Title.Should().Be("Jutarnji trening");
        response.TrainerName.Should().Be("Sara");
        response.Location.Should().BeEmpty();
        response.IsCancelled.Should().BeFalse();

        var storedTraining = await dbContext.TrainingSessions.SingleAsync();
        storedTraining.Title.Should().Be("Jutarnji trening");
        storedTraining.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        storedTraining.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateTrainingAsync_WhenStartTimeIsInPast_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var startTime = DateTime.UtcNow.AddDays(-1);

        var act = () => trainingService.CreateTrainingAsync(
            new CreateTrainingSessionRequest
            {
                Title = "Trening",
                StartTime = startTime,
                EndTime = startTime.AddHours(1),
                Capacity = 10
            });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Vreme početka mora biti u budućnosti.");
    }

    [Fact]
    public async Task UpdateTrainingAsync_ShouldUpdateTrainingFields()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Stari trening");
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();
        var newStartTime = DateTime.UtcNow.AddDays(2);

        var response = await trainingService.UpdateTrainingAsync(
            training.Id,
            new UpdateTrainingSessionRequest
            {
                Title = "Novi trening",
                Description = "Opis",
                StartTime = newStartTime,
                EndTime = newStartTime.AddHours(1),
                Capacity = 8,
                IsCancelled = true,
                CancellationReason = "Pomeranje termina"
            });

        response.Title.Should().Be("Novi trening");
        response.Description.Should().Be("Opis");
        response.Capacity.Should().Be(8);
        response.IsCancelled.Should().BeTrue();
        response.CancellationReason.Should().Be("Pomeranje termina");
    }

    [Fact]
    public async Task CancelTrainingAsync_ShouldMarkTrainingAsCancelled()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Trening");
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var response = await trainingService.CancelTrainingAsync(training.Id, "Sara je odsutna");

        response.IsCancelled.Should().BeTrue();
        response.CancellationReason.Should().Be("Sara je odsutna");
    }

    [Fact]
    public async Task CancelTrainingAsync_WhenReasonIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Trening");
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => trainingService.CancelTrainingAsync(training.Id);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Razlog otkazivanja je obavezan.");
    }

    [Fact]
    public async Task DeleteTrainingAsync_ShouldRemoveTraining()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Trening");
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        await trainingService.DeleteTrainingAsync(training.Id);

        var trainingExists = await dbContext.TrainingSessions.AnyAsync(x => x.Id == training.Id);
        trainingExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTrainingAsync_WhenTrainingHasReservations_ShouldThrowConflict()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var trainingService = services.GetRequiredService<ITrainingService>();
        var training = CreateTraining(DateTime.UtcNow.AddDays(1), "Trening");
        training.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainingSessionId = training.Id,
            Status = ReservationStatus.Reserved
        });
        dbContext.TrainingSessions.Add(training);
        await dbContext.SaveChangesAsync();

        var act = () => trainingService.DeleteTrainingAsync(training.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Trening sa rezervacijama ne može biti obrisan.");
    }

    [Fact]
    public async Task GetTrainingByIdAsync_WhenTrainingDoesNotExist_ShouldThrowNotFound()
    {
        var services = CreateServiceProvider();
        var trainingService = services.GetRequiredService<ITrainingService>();

        var act = () => trainingService.GetTrainingByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Trening nije pronađen.");
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<ITrainingService, TrainingService>();

        return services.BuildServiceProvider();
    }

    private static TrainingSession CreateTraining(DateTime startTime, string title)
    {
        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = title,
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
}
