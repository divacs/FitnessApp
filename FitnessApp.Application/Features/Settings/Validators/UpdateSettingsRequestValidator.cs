using FitnessApp.Application.Features.Settings.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Settings.Validators;

public class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.CancellationDeadlineHours)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Rok za otkazivanje ne može biti negativan.");

        RuleFor(x => x.ContactPhone)
            .MaximumLength(100)
            .WithMessage("Kontakt telefon može imati najviše 100 karaktera.");

        RuleFor(x => x.DefaultTrainingCapacity)
            .GreaterThan(0)
            .WithMessage("Podrazumevani kapacitet treninga mora biti veći od 0.");

        RuleFor(x => x.AutoMarkAttendanceDelayMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Delay za automatsko označavanje dolaska ne može biti negativan.");
    }
}
