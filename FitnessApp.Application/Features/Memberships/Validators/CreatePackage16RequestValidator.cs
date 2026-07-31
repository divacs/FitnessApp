using FitnessApp.Application.Features.Memberships.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Memberships.Validators;

public class CreatePackage16RequestValidator : AbstractValidator<CreatePackage16Request>
{
    public CreatePackage16RequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Datum početka je obavezan.");
    }
}
