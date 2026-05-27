using FitnessApp.Application.Features.Reservations.DTOs;
using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Reservations.Mappings;

public static class ReservationMappings
{
    public static ReservationResponse ToResponse(this Reservation reservation)
    {
        return new ReservationResponse
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            UserFullName = reservation.User is null ? string.Empty : reservation.User.FullName,
            UserEmail = reservation.User?.Email ?? string.Empty,
            TrainingSessionId = reservation.TrainingSessionId,
            TrainingTitle = reservation.TrainingSession.Title,
            TrainingStartTime = reservation.TrainingSession.StartTime,
            TrainingEndTime = reservation.TrainingSession.EndTime,
            TrainerName = reservation.TrainingSession.TrainerName,
            Location = reservation.TrainingSession.Location,
            Status = reservation.Status,
            ReservedAt = reservation.ReservedAt,
            CancelledAt = reservation.CancelledAt,
            CancelledByUser = reservation.CancelledByUser,
            CancelledByAdmin = reservation.CancelledByAdmin,
            AttendedAt = reservation.AttendedAt,
            NoShowAt = reservation.NoShowAt,
            ReminderSentAt = reservation.ReminderSentAt,
            AutoMarkedAttended = reservation.AutoMarkedAttended,
            AutoMarkedAt = reservation.AutoMarkedAt,
            Notes = reservation.Notes
        };
    }
}
