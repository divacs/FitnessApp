using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Pagination;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Features.Memberships.DTOs;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Identity;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class UserService : IUserService
{
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
        var utcNow = DateTime.UtcNow;
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted)
            .WhereIf(status.HasValue, user => user.UserStatus == status!.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(user =>
                user.FirstName.ToLower().Contains(normalizedSearch)
                || user.LastName.ToLower().Contains(normalizedSearch)
                || (user.FirstName + " " + user.LastName).ToLower().Contains(normalizedSearch)
                || (user.LastName + " " + user.FirstName).ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ApplyPagination(page, pageSize)
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
                CreatedAt = user.CreatedAt,
                ActivePackage = _dbContext.UserTrainingBalances
                    .Where(balance =>
                        balance.UserId == user.Id
                        && balance.IsActive
                        && !balance.IsExpired
                        && balance.RemainingSessions > 0
                        && (
                            ((balance.PurchaseType == PurchaseType.Package6
                                || balance.PurchaseType == PurchaseType.Package12
                                || balance.PurchaseType == PurchaseType.Package16)
                                && balance.StartDate <= utcNow
                                && balance.EndDate >= utcNow
                                && _dbContext.Payments.Any(payment =>
                                    payment.UserId == balance.UserId
                                    && payment.PaymentType == balance.PurchaseType
                                    && (payment.StartDate == balance.StartDate || payment.StartDate == null)))
                            || (balance.PurchaseType == PurchaseType.SingleSessions
                                && _dbContext.Payments.Any(payment =>
                                    payment.UserId == balance.UserId
                                    && payment.PaymentType == PurchaseType.SingleSessions))))
                    .OrderBy(balance => balance.PurchaseType == PurchaseType.SingleSessions)
                    .ThenBy(balance => balance.EndDate)
                    .ThenByDescending(balance => balance.CreatedAt)
                    .Select(balance => new UserTrainingBalanceResponse
                    {
                        Id = balance.Id,
                        UserId = balance.UserId,
                        PurchaseType = balance.PurchaseType,
                        TotalSessions = balance.TotalSessions,
                        RemainingSessions = balance.RemainingSessions,
                        StartDate = balance.StartDate,
                        EndDate = balance.EndDate,
                        IsActive = balance.IsActive,
                        IsExpired = balance.IsExpired,
                        CarriedOverSessions = balance.CarriedOverSessions,
                        ExpirationReminderSentAt = balance.ExpirationReminderSentAt,
                        CreatedAt = balance.CreatedAt,
                        Notes = balance.Notes
                    })
                    .FirstOrDefault(),
                TotalRemainingSessions = _dbContext.UserTrainingBalances
                    .Where(balance =>
                        balance.UserId == user.Id
                        && balance.IsActive
                        && !balance.IsExpired
                        && balance.RemainingSessions > 0
                        && ((balance.PurchaseType == PurchaseType.Package6
                                || balance.PurchaseType == PurchaseType.Package12
                                || balance.PurchaseType == PurchaseType.Package16)
                            ? balance.StartDate <= utcNow
                                && balance.EndDate >= utcNow
                                && _dbContext.Payments.Any(payment =>
                                    payment.UserId == balance.UserId
                                    && payment.PaymentType == balance.PurchaseType
                                    && (payment.StartDate == balance.StartDate || payment.StartDate == null))
                            : balance.PurchaseType == PurchaseType.SingleSessions
                                && _dbContext.Payments.Any(payment =>
                                    payment.UserId == balance.UserId
                                    && payment.PaymentType == balance.PurchaseType)))
                    .Sum(balance => (int?)balance.RemainingSessions) ?? 0
            })
            .ToListAsync(cancellationToken);

        return users.ToPaginatedResponse(page, pageSize, totalCount);
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
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.IsDeleted)
        {
            throw new NotFoundException("Korisnik nije pronađen.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new BadRequestException(
                "Promena lozinke nije uspela.",
                ["Lozinke se ne podudaraju."]);
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            throw result.ToBadRequestException("Promena lozinke nije uspela.");
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
        var utcNow = DateTime.UtcNow;

        user.UserStatus = UserStatus.Blocked;
        user.BlockedAt = utcNow;
        user.UpdatedAt = utcNow;

        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} blocked by admin and {RefreshTokenCount} active refresh tokens were revoked.",
            user.Id,
            activeRefreshTokens.Count);
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
}
