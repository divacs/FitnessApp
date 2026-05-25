using System.Security.Claims;

namespace FitnessApp.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : throw new UnauthorizedAccessException("Korisnik nije autentifikovan.");
    }

    public static string GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Email claim nije pronađen.");
    }
}
