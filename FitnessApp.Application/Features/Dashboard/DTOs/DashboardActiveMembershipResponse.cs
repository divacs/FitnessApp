using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Dashboard.DTOs;

public class DashboardActiveMembershipResponse
{
    public Guid? PaymentId { get; init; }

    public PurchaseType PaymentType { get; init; }

    public int NumberOfSessions { get; init; }

    public int RemainingSessions { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string Status { get; init; } = string.Empty;
}
