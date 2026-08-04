using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email je obavezan.")
            .EmailAddress()
            .WithMessage("Email nije u validnom formatu.");

        RuleFor(x => x.ResetToken)
            .NotEmpty()
            .WithMessage("Reset token je obavezan.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Nova lozinka je obavezna.")
            .MinimumLength(8)
            .WithMessage("Nova lozinka mora imati najmanje 8 karaktera.");

        RuleFor(x => x.PasswordConfirmation)
            .NotEmpty()
            .WithMessage("Potvrda lozinke je obavezna.")
            .Equal(x => x.NewPassword)
            .WithMessage("Lozinke se ne podudaraju.");
    }
}
