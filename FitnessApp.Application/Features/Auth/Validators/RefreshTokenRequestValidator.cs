using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token je obavezan.");
    }
}
