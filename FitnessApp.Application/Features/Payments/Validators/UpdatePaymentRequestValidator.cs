using FitnessApp.Application.Features.Payments.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Payments.Validators;

public class UpdatePaymentRequestValidator : AbstractValidator<UpdatePaymentRequest>
{
    public UpdatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Iznos ne može biti negativan.");

        RuleFor(x => x.PaymentDate)
            .NotEmpty()
            .WithMessage("Datum uplate je obavezan.");
    }
}
