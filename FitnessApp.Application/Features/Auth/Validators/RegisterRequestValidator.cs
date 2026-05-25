using FitnessApp.Application.Features.Auth.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Ime je obavezno.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Prezime je obavezno.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email je obavezan.")
            .EmailAddress()
            .WithMessage("Email nije validan.");

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithMessage("Lozinka mora imati najmanje 8 karaktera.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Broj telefona je obavezan.");
    }
}
