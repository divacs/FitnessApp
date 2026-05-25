using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;

namespace FitnessApp.API.Extensions;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyConstants.AdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleConstants.Admin);
            });

            options.AddPolicy(AuthorizationPolicyConstants.VerifiedUsersOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(AuthClaimConstants.UserStatus, UserStatus.Verified.ToString());
            });
        });

        return services;
    }
}
