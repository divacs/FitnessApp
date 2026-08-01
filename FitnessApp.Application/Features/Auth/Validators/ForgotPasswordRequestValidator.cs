using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email je obavezan.")
            .EmailAddress()
            .WithMessage("Email nije u validnom formatu.");
    }
}
