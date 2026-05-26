using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Memberships.Mappings;

public static class MembershipMappings
{
    public static UserTrainingBalanceResponse ToResponse(this UserTrainingBalance balance)
    {
        return new UserTrainingBalanceResponse
        {
            Id = balance.Id,
            UserId = balance.UserId,
            PurchaseType = balance.PurchaseType,
            TotalSessions = balance.TotalSessions,
            RemainingSessions = balance.RemainingSessions,
            StartDate = balance.StartDate,
            EndDate = balance.EndDate,
            IsActive = balance.IsActive,
            IsExpired = balance.IsExpired,
            CarriedOverSessions = balance.CarriedOverSessions,
            ExpirationReminderSentAt = balance.ExpirationReminderSentAt,
            CreatedAt = balance.CreatedAt,
            Notes = balance.Notes
        };
    }

    public static BalanceHistoryResponse ToHistoryResponse(this UserTrainingBalance balance)
    {
        return new BalanceHistoryResponse
        {
            Id = balance.Id,
            UserId = balance.UserId,
            PurchaseType = balance.PurchaseType,
            TotalSessions = balance.TotalSessions,
            RemainingSessions = balance.RemainingSessions,
            StartDate = balance.StartDate,
            EndDate = balance.EndDate,
            IsActive = balance.IsActive,
            IsExpired = balance.IsExpired,
            CarriedOverSessions = balance.CarriedOverSessions,
            CreatedAt = balance.CreatedAt,
            Notes = balance.Notes
        };
    }
}
