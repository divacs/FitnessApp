using FitnessApp.Application.Features.Trainings.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Trainings.Validators;

public class CreateTrainingSessionRequestValidator : AbstractValidator<CreateTrainingSessionRequest>
{
    public CreateTrainingSessionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Naziv treninga je obavezan.");

        RuleFor(x => x.StartTime)
            .NotEmpty()
            .WithMessage("Vreme početka je obavezno.")
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("Vreme početka mora biti u budućnosti.");

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .WithMessage("Vreme završetka je obavezno.")
            .GreaterThan(x => x.StartTime)
            .WithMessage("Vreme završetka mora biti nakon vremena početka.");

        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Kapacitet ne može biti negativan.");
    }
}
