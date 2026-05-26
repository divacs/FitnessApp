using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Payments.DTOs;

public class PaymentResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string UserFullName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime PaymentDate { get; init; }

    public PurchaseType PaymentType { get; init; }

    public int NumberOfSessions { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public Guid? CreatedByAdminId { get; init; }
}
