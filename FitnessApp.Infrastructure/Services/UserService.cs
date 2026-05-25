using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class UserService : IUserService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext dbContext,
        IEmailService emailService,
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PaginatedResponse<UserListResponse>> GetUsersAsync(
        int page,
        int pageSize,
        UserStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);

        var query = _dbContext.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(user => user.UserStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(user =>
                user.FirstName.Contains(normalizedSearch)
                || user.LastName.Contains(normalizedSearch)
                || (user.Email != null && user.Email.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(user => new UserListResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FirstName + " " + user.LastName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                UserStatus = user.UserStatus,
                VerifiedAt = user.VerifiedAt,
                BlockedAt = user.BlockedAt,
                UnblockedAt = user.UnblockedAt,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<UserListResponse>(
            users,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<UserProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        return MapUserProfileResponse(user);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated profile.", user.Id);

        return MapUserProfileResponse(user);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.IsDeleted)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            throw CreateBadRequestException("Promena lozinke nije uspela.", result);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} changed password.", user.Id);
    }

    public async Task VerifyUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        user.UserStatus = UserStatus.Verified;
        user.VerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _emailService.SendUserVerifiedEmailAsync(user.Email ?? string.Empty, user.FirstName, cancellationToken);

        _logger.LogInformation("User {UserId} verified by admin.", user.Id);
    }

    public async Task BlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        user.UserStatus = UserStatus.Blocked;
        user.BlockedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} blocked by admin.", user.Id);
    }

    public async Task UnblockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        user.UserStatus = UserStatus.Verified;
        user.UnblockedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} unblocked by admin.", user.Id);
    }

    private async Task<ApplicationUser> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        return user;
    }

    private static UserProfileResponse MapUserProfileResponse(ApplicationUser user)
    {
        return new UserProfileResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            UserStatus = user.UserStatus,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static BadRequestException CreateBadRequestException(string message, IdentityResult result)
    {
        var errors = result.Errors
            .Select(error => error.Description)
            .ToArray();

        return new BadRequestException(message, errors);
    }
}
