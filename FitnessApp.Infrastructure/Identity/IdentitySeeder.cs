using FitnessApp.Application.Settings;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FitnessApp.Infrastructure.Identity;

public class IdentitySeeder : IIdentitySeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AdminSeedSettings _adminSeedSettings;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<AdminSeedSettings> adminSeedSettings,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _adminSeedSettings = adminSeedSettings.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in ApplicationRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

            if (!result.Succeeded)
            {
                throw CreateIdentityException($"Failed to create role '{roleName}'.", result);
            }
        }
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        ValidateAdminSeedSettings();

        var adminUser = await _userManager.FindByEmailAsync(_adminSeedSettings.Email);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = _adminSeedSettings.Email,
                Email = _adminSeedSettings.Email,
                FirstName = _adminSeedSettings.FirstName,
                LastName = _adminSeedSettings.LastName,
                UserStatus = UserStatus.Verified,
                EmailConfirmed = true,
                VerifiedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(adminUser, _adminSeedSettings.Password);

            if (!createResult.Succeeded)
            {
                throw CreateIdentityException("Failed to create default admin user.", createResult);
            }

            _logger.LogInformation("Default admin user seeded.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (await _userManager.IsInRoleAsync(adminUser, ApplicationRoles.Admin))
        {
            return;
        }

        var addRoleResult = await _userManager.AddToRoleAsync(adminUser, ApplicationRoles.Admin);

        if (!addRoleResult.Succeeded)
        {
            throw CreateIdentityException("Failed to add default admin user to Admin role.", addRoleResult);
        }
    }

    private void ValidateAdminSeedSettings()
    {
        if (string.IsNullOrWhiteSpace(_adminSeedSettings.Email)
            || string.IsNullOrWhiteSpace(_adminSeedSettings.Password)
            || string.IsNullOrWhiteSpace(_adminSeedSettings.FirstName)
            || string.IsNullOrWhiteSpace(_adminSeedSettings.LastName))
        {
            throw new InvalidOperationException("AdminSeed:Email, AdminSeed:Password, AdminSeed:FirstName and AdminSeed:LastName are required.");
        }
    }

    private static InvalidOperationException CreateIdentityException(string message, IdentityResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));

        return new InvalidOperationException($"{message} {errors}");
    }
}
