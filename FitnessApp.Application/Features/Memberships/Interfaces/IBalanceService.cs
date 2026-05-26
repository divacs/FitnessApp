using FitnessApp.Application.Features.Memberships.DTOs;

namespace FitnessApp.Application.Features.Memberships.Interfaces;

public interface IBalanceService
{
    Task<CurrentBalanceResponse> GetCurrentBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BalanceHistoryResponse>> GetBalanceHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserTrainingBalanceResponse>> GetUserBalancesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserTrainingBalanceResponse> CreatePackage12Async(
        Guid userId,
        CreatePackage12Request request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<UserTrainingBalanceResponse> CreatePackage6Async(
        Guid userId,
        CreatePackage6Request request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<UserTrainingBalanceResponse> AddSingleSessionsAsync(
        Guid userId,
        AddSingleSessionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task ConsumeSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserTrainingBalanceResponse> UpdateBalanceAsync(
        Guid balanceId,
        UpdateBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteBalanceAsync(
        Guid balanceId,
        CancellationToken cancellationToken = default);
}
