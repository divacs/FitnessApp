using FitnessApp.Application.Features.Memberships.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Memberships.Validators;

public class AddSingleSessionsRequestValidator : AbstractValidator<AddSingleSessionsRequest>
{
    public AddSingleSessionsRequestValidator()
    {
        RuleFor(x => x.NumberOfSessions)
            .NotEmpty()
            .WithMessage("Broj termina je obavezan.")
            .GreaterThan(0)
            .WithMessage("Broj termina mora biti veći od 0.");
    }
}
