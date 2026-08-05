using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Payments.Mappings;

public static class PaymentMappings
{
    public static PaymentResponse ToResponse(this Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            UserId = payment.UserId,
            UserFullName = payment.User.FullName,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            StartDate = payment.StartDate,
            PaymentType = payment.PaymentType,
            PackageName = payment.PaymentType switch
            {
                PurchaseType.Package6 => "Paket 6",
                PurchaseType.Package12 => "Paket 12",
                PurchaseType.Package16 => "Paket 16",
                PurchaseType.SingleSessions => "Pojedinačni termini",
                _ => payment.PaymentType.ToString()
            },
            NumberOfSessions = payment.NumberOfSessions,
            Note = payment.Note,
            CreatedAt = payment.CreatedAt,
            CreatedByAdminId = payment.CreatedByAdminId
        };
    }
}
