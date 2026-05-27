using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class RevokeTokenRequestValidator : AbstractValidator<RevokeTokenRequest>
{
    public RevokeTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token je obavezan.");
    }
}
