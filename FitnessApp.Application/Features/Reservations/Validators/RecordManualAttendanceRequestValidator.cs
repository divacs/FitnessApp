using FitnessApp.Application.Features.Reservations.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Reservations.Validators;

public class RecordManualAttendanceRequestValidator : AbstractValidator<RecordManualAttendanceRequest>
{
    public RecordManualAttendanceRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Korisnik je obavezan.");

        RuleFor(x => x.TrainingSessionId)
            .NotEmpty()
            .WithMessage("Trening je obavezan.");
    }
}
