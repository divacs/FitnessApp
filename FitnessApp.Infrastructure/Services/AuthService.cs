using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Auth.DTOs;
using FitnessApp.Application.Features.Auth.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext dbContext,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
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

        return await MapCurrentUserResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
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

    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
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
            throw new BadRequestException("Refresh token je opozvan.");
        }

        EnsureUserCanAuthenticate(refreshToken.User);

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

        var user = await _userManager.FindByIdAsync(userId.ToString());

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
