using FitnessApp.Application.Features.Users.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Users.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Ime je obavezno.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Prezime je obavezno.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Broj telefona je obavezan.");
    }
}
