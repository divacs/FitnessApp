using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email je obavezan.")
            .EmailAddress()
            .WithMessage("Email nije validan.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Lozinka je obavezna.");
    }
}
