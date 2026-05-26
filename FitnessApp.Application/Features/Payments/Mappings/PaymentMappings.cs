using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Domain.Entities;

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
            PaymentType = payment.PaymentType,
            NumberOfSessions = payment.NumberOfSessions,
            Note = payment.Note,
            CreatedAt = payment.CreatedAt,
            CreatedByAdminId = payment.CreatedByAdminId
        };
    }
}
