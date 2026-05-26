using FitnessApp.Application.Features.Memberships.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Memberships.Validators;

public class UpdateBalanceRequestValidator : AbstractValidator<UpdateBalanceRequest>
{
    public UpdateBalanceRequestValidator()
    {
        RuleFor(x => x.RemainingSessions)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Preostali broj termina ne može biti negativan.");
    }
}
