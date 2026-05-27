using FitnessApp.Application.Features.Terms.DTOs;
using FluentValidation;

namespace FitnessApp.Application.Features.Terms.Validators;

public class UpdateTermsRequestValidator : AbstractValidator<UpdateTermsRequest>
{
    public UpdateTermsRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Sadržaj opštih uslova je obavezan.");
    }
}
