using FitnessApp.Application.Features.Trainings.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Trainings.Validators;

public class CancelTrainingSessionRequestValidator : AbstractValidator<CancelTrainingSessionRequest>
{
    public CancelTrainingSessionRequestValidator()
    {
        RuleFor(x => x.CancellationReason)
            .NotEmpty()
            .WithMessage("Razlog otkazivanja je obavezan.");
    }
}
