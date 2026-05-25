using FitnessApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace FitnessApp.Infrastructure.Identity;

internal static class IdentityResultExtensions
{
    public static BadRequestException ToBadRequestException(
        this IdentityResult result,
        string message)
    {
        var errors = result.Errors
            .Select(error => error.Description)
            .ToArray();

        return new BadRequestException(message, errors);
    }
}
