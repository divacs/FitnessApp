using FitnessApp.Application.Features.Notifications.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Notifications.Validators;

public class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Naslov je obavezan.")
            .MaximumLength(150)
            .WithMessage("Naslov može imati najviše 150 karaktera.");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Poruka je obavezna.")
            .MaximumLength(2000)
            .WithMessage("Poruka može imati najviše 2000 karaktera.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Tip notifikacije nije validan.");
    }
}
