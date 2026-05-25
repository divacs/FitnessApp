using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Users.Interfaces;

public interface IUserService
{
    Task<PaginatedResponse<UserListResponse>> GetUsersAsync(
        int page,
        int pageSize,
        UserStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task VerifyUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task BlockUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UnblockUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
