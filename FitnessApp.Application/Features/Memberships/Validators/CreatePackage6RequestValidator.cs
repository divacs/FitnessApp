using FitnessApp.Application.Features.Memberships.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Memberships.Validators;

public class CreatePackage6RequestValidator : AbstractValidator<CreatePackage6Request>
{
    public CreatePackage6RequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Datum početka je obavezan.");
    }
}
