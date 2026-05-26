using FitnessApp.Application.Features.Reservations.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Reservations.Validators;

public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.TrainingSessionId)
            .NotEmpty()
            .WithMessage("Trening je obavezan.");
    }
}
