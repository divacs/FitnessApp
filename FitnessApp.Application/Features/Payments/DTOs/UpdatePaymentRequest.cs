namespace FitnessApp.Application.Features.Payments.DTOs;

public class UpdatePaymentRequest
{
    public decimal Amount { get; init; }

    public DateTime PaymentDate { get; init; }

    public string? Note { get; init; }
}
