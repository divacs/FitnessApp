using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Domain.Enums;
using FluentValidation;

namespace FitnessApp.Application.Features.Payments.Validators;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Korisnik je obavezan.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Iznos ne može biti negativan.");

        RuleFor(x => x.PaymentType)
            .IsInEnum()
            .WithMessage("Tip uplate nije validan.");

        RuleFor(x => x.NumberOfSessions)
            .NotNull()
            .WithMessage("Broj termina je obavezan za pojedinačne termine.")
            .GreaterThan(0)
            .WithMessage("Broj termina mora biti veći od 0.")
            .When(x => x.PaymentType == PurchaseType.SingleSessions);

        RuleFor(x => x.StartDate)
            .NotNull()
            .WithMessage("Datum početka je obavezan za paket.")
            .When(x => x.PaymentType is PurchaseType.Package12 or PurchaseType.Package6);
    }
}
