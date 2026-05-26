using FitnessApp.Application.Features.Memberships.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Memberships.Validators;

public class CreatePackage12RequestValidator : AbstractValidator<CreatePackage12Request>
{
    public CreatePackage12RequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Datum početka je obavezan.");
    }
}
