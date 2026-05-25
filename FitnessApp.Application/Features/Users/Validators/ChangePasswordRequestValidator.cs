using FitnessApp.Application.Features.Users.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Users.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Trenutna lozinka je obavezna.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Nova lozinka je obavezna.")
            .MinimumLength(8)
            .WithMessage("Nova lozinka mora imati najmanje 8 karaktera.");
    }
}
