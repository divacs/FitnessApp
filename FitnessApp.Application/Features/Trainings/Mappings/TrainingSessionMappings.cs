using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Trainings.Mappings;

public static class TrainingSessionMappings
{
    public static TrainingSessionResponse ToResponse(this TrainingSession trainingSession)
    {
        var reservedCount = trainingSession.GetReservedCount();

        return new TrainingSessionResponse
        {
            Id = trainingSession.Id,
            Title = trainingSession.Title,
            Description = trainingSession.Description,
            StartTime = trainingSession.StartTime,
            EndTime = trainingSession.EndTime,
            Capacity = trainingSession.Capacity,
            ReservedCount = reservedCount,
            AvailableSpots = GetAvailableSpots(trainingSession.Capacity, reservedCount),
            TrainerName = trainingSession.TrainerName,
            Location = trainingSession.Location,
            IsCancelled = trainingSession.IsCancelled,
            CancellationReason = trainingSession.CancellationReason
        };
    }

    public static TrainingCalendarResponse ToCalendarResponse(this TrainingSession trainingSession)
    {
        var reservedCount = trainingSession.GetReservedCount();

        return new TrainingCalendarResponse
        {
            Id = trainingSession.Id,
            Title = trainingSession.Title,
            StartTime = trainingSession.StartTime,
            EndTime = trainingSession.EndTime,
            Capacity = trainingSession.Capacity,
            ReservedCount = reservedCount,
            AvailableSpots = GetAvailableSpots(trainingSession.Capacity, reservedCount),
            TrainerName = trainingSession.TrainerName,
            Location = trainingSession.Location,
            IsCancelled = trainingSession.IsCancelled
        };
    }

    private static int GetReservedCount(this TrainingSession trainingSession)
    {
        return trainingSession.Reservations
            .Count(reservation => reservation.Status == ReservationStatus.Reserved);
    }

    private static int GetAvailableSpots(int capacity, int reservedCount)
    {
        return Math.Max(capacity - reservedCount, 0);
    }
}
