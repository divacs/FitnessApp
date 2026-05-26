using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Memberships.DTOs;

public class BalanceHistoryResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public PurchaseType PurchaseType { get; init; }

    public int TotalSessions { get; init; }

    public int RemainingSessions { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public bool IsActive { get; init; }

    public bool IsExpired { get; init; }

    public int CarriedOverSessions { get; init; }

    public DateTime CreatedAt { get; init; }

    public string? Notes { get; init; }
}
