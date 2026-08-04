using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Auth.Interfaces;
using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Settings;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace FitnessApp.Infrastructure.Services;

/// <summary>
/// Implements registration, login, password recovery, refresh-token rotation, logout, and current-user retrieval.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly AppSettings _appSettings;
    private readonly AdminSeedSettings _adminSeedSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext dbContext,
        ITokenService tokenService,
        IEmailService emailService,
        IOptions<AppSettings> appSettings,
        IOptions<AdminSeedSettings> adminSeedSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _emailService = emailService;
        _appSettings = appSettings.Value;
        _adminSeedSettings = adminSeedSettings.Value;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            throw new ConflictException("Korisnik sa ovom email adresom već postoji.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            UserStatus = UserStatus.Unverified,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            throw createResult.ToBadRequestException("Registracija nije uspela.");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, RoleConstants.User);

        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign default role to user {UserId}.", user.Id);
            throw roleResult.ToBadRequestException("Dodela korisničke role nije uspela.");
        }

        await SendAdminRegistrationNotificationAsync(user, cancellationToken);

        return await MapCurrentUserResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || user.IsDeleted)
        {
            throw new BadRequestException("Email ili lozinka nisu ispravni.");
        }

        var passwordSignInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!passwordSignInResult.Succeeded)
        {
            throw new BadRequestException("Email ili lozinka nisu ispravni.");
        }

        EnsureUserCanAuthenticate(user);

        return await CreateAuthResponseAndRefreshTokenAsync(user, cancellationToken: cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || user.IsDeleted || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = BuildPasswordResetUrl(user.Email, resetToken);

        await _emailService.SendPasswordResetEmailAsync(
            user.Email,
            user.FirstName,
            resetUrl,
            cancellationToken);

        _logger.LogInformation("Password reset email requested for user {UserId}.", user.Id);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || user.IsDeleted)
        {
            throw new BadRequestException(
                "Reset lozinke nije uspeo.",
                ["Link za reset lozinke nije validan ili je istekao."]);
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            request.ResetToken,
            request.NewPassword);

        if (!result.Succeeded)
        {
            throw MapResetPasswordFailure(result);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var refreshToken = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null)
        {
            throw new BadRequestException("Refresh token nije validan.");
        }

        if (refreshToken.IsExpired)
        {
            throw new BadRequestException("Refresh token je istekao.");
        }

        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning(
                "Rejected reused or revoked refresh token for user {UserId}.",
                refreshToken.UserId);

            await RevokeActiveRefreshTokensAsync(refreshToken.UserId, cancellationToken);

            throw new BadRequestException("Refresh token je već iskorišćen ili opozvan.");
        }

        await EnsureUserCanRefreshAsync(refreshToken.User, cancellationToken);

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByToken = newRefreshToken;

        return await CreateAuthResponseAndRefreshTokenAsync(
            refreshToken.User,
            newRefreshToken,
            cancellationToken);
    }

    public async Task RevokeTokenAsync(
        RevokeTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            return;
        }

        refreshToken.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token revoked for user {UserId}.", refreshToken.UserId);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        return await MapCurrentUserResponseAsync(user);
    }

    private async Task<AuthResponse> CreateAuthResponseAndRefreshTokenAsync(
        ApplicationUser user,
        string? refreshTokenValue = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
        var accessTokenExpiresAt = _tokenService.GetAccessTokenExpiration();
        var refreshToken = refreshTokenValue ?? _tokenService.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _tokenService.GetRefreshTokenExpiration(),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var role = await GetPrimaryRoleAsync(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = accessTokenExpiresAt,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = role,
            UserStatus = user.UserStatus
        };
    }

    private async Task<CurrentUserResponse> MapCurrentUserResponseAsync(ApplicationUser user)
    {
        var role = await GetPrimaryRoleAsync(user);

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = role,
            UserStatus = user.UserStatus
        };
    }

    private async Task<string> GetPrimaryRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return roles.FirstOrDefault() ?? string.Empty;
    }

    private string BuildPasswordResetUrl(string email, string resetToken)
    {
        var frontendUrl = _appSettings.FrontendUrl.TrimEnd('/');
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(resetToken);

        return $"{frontendUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
    }

    private static BadRequestException MapResetPasswordFailure(IdentityResult result)
    {
        var errors = result.Errors
            .Select(error => error.Code == "InvalidToken"
                ? "Link za reset lozinke nije validan ili je istekao."
                : error.Description)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new BadRequestException("Reset lozinke nije uspelo.", errors);
    }

    private async Task SendAdminRegistrationNotificationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var registrationTimestamp = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var adminEmails = (await _userManager.GetUsersInRoleAsync(RoleConstants.Admin))
            .Where(admin => !admin.IsDeleted && !string.IsNullOrWhiteSpace(admin.Email))
            .Select(admin => admin.Email!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (adminEmails.Count == 0 && !string.IsNullOrWhiteSpace(_adminSeedSettings.Email))
        {
            adminEmails.Add(_adminSeedSettings.Email);
        }

        if (adminEmails.Count == 0)
        {
            return;
        }

        var htmlBody = BuildAdminRegistrationNotificationHtml(fullName, user.Email ?? string.Empty, registrationTimestamp);
        var plainTextBody = BuildAdminRegistrationNotificationPlainText(fullName, user.Email ?? string.Empty, registrationTimestamp);

        foreach (var adminEmail in adminEmails)
        {
            await _emailService.SendAsync(
                adminEmail,
                "Novi korisnik se registrovao",
                htmlBody,
                plainTextBody,
                cancellationToken);
        }
    }

    private static string BuildAdminRegistrationNotificationHtml(
        string fullName,
        string email,
        string registrationTimestamp)
    {
        var encodedFullName = WebUtility.HtmlEncode(fullName);
        var encodedEmail = WebUtility.HtmlEncode(email);
        var encodedRegisteredAt = WebUtility.HtmlEncode(registrationTimestamp);

        return $$"""
            <!doctype html>
            <html lang="sr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Novi korisnik se registrovao</title>
            </head>
            <body style="margin:0;padding:0;background:#FFF8F3;font-family:Arial,sans-serif;color:#2F2933;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#FFF8F3;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#FFFFFF;border-radius:8px;padding:28px;border:1px solid #F3E6DD;">
                      <tr>
                        <td>
                          <p style="margin:0 0 8px;color:#9B6EF3;font-size:14px;font-weight:bold;">Sara - FitnessApp</p>
                          <h1 style="margin:0 0 18px;font-size:24px;line-height:1.25;color:#2F2933;">Novi korisnik se registrovao</h1>
                          <p style="margin:0 0 12px;font-size:16px;line-height:1.6;">Potrebno je da ga verifikujete.</p>
                          <p style="margin:0 0 8px;font-size:16px;line-height:1.6;"><strong>Ime i prezime:</strong> {{encodedFullName}}</p>
                          <p style="margin:0 0 8px;font-size:16px;line-height:1.6;"><strong>Email:</strong> {{encodedEmail}}</p>
                          <p style="margin:0;font-size:16px;line-height:1.6;"><strong>Datum registracije:</strong> {{encodedRegisteredAt}}</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildAdminRegistrationNotificationPlainText(
        string fullName,
        string email,
        string registrationTimestamp)
    {
        return $"""
            novi koisnik se registrovao, potrebno je da ga verifikujete.

            Ime i prezime: {fullName}
            Email: {email}
            Datum registracije: {registrationTimestamp}
            """;
    }

    private async Task EnsureUserCanRefreshAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (user.IsDeleted)
        {
            await RevokeActiveRefreshTokensAsync(user.Id, cancellationToken);
            throw new ForbiddenException("Korisnički nalog više nije dostupan.");
        }

        if (user.UserStatus == UserStatus.Blocked)
        {
            await RevokeActiveRefreshTokensAsync(user.Id, cancellationToken);
            throw new ForbiddenException("Korisnik je blokiran.");
        }

        if (user.UserStatus != UserStatus.Verified)
        {
            await RevokeActiveRefreshTokensAsync(user.Id, cancellationToken);
            throw new ForbiddenException("Korisnik još nije verifikovan.");
        }
    }

    private async Task<int> RevokeActiveRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count == 0)
        {
            return 0;
        }

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Revoked {RefreshTokenCount} active refresh tokens for user {UserId}.",
            activeTokens.Count,
            userId);

        return activeTokens.Count;
    }

    private static void EnsureUserCanAuthenticate(ApplicationUser user)
    {
        if (user.UserStatus == UserStatus.Blocked)
        {
            throw new ForbiddenException("Korisnik je blokiran.");
        }

        if (user.UserStatus != UserStatus.Verified)
        {
            throw new ForbiddenException("Korisnik još nije verifikovan.");
        }
    }
}
