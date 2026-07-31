using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Memberships.DTOs;

public class MembershipHistoryResponse
{
    public Guid Id { get; init; }

    public PurchaseType PurchaseType { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public DateTime StartDate { get; init; }

    public DateTime? PaymentDate { get; init; }

    public DateTime? EndDate { get; init; }

    public int TotalSessions { get; init; }

    public int RemainingSessions { get; init; }

    public bool IsCurrentlyActive { get; init; }
}
