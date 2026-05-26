using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Payments.DTOs;

public class CreatePaymentRequest
{
    public Guid UserId { get; init; }

    public decimal Amount { get; init; }

    public DateTime PaymentDate { get; init; }

    public PurchaseType PaymentType { get; init; }

    public int? NumberOfSessions { get; init; }

    public string? Note { get; init; }

    public DateTime? StartDate { get; init; }
}
