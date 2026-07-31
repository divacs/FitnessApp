using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;

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

    public static MembershipHistoryResponse ToMembershipHistoryResponse(
        this UserTrainingBalance balance,
        DateTime? paymentDate,
        bool isCurrentlyActive)
    {
        return new MembershipHistoryResponse
        {
            Id = balance.Id,
            PurchaseType = balance.PurchaseType,
            PackageName = ResolvePackageName(balance.PurchaseType),
            StartDate = balance.StartDate,
            PaymentDate = paymentDate,
            EndDate = balance.EndDate,
            TotalSessions = balance.TotalSessions,
            RemainingSessions = balance.RemainingSessions,
            IsCurrentlyActive = isCurrentlyActive
        };
    }

    private static string ResolvePackageName(PurchaseType purchaseType)
    {
        return purchaseType switch
        {
            PurchaseType.Package6 => "Paket 6",
            PurchaseType.Package12 => "Paket 12",
            PurchaseType.Package16 => "Paket 16",
            PurchaseType.SingleSessions => "Pojedinačni termini",
            _ => purchaseType.ToString()
        };
    }
}
