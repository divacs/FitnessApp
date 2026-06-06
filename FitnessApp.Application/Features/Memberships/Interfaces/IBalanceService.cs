using FitnessApp.Application.Features.Memberships.DTOs;

namespace FitnessApp.Application.Features.Memberships.Interfaces;

/// <summary>
/// Manages packages, single sessions, carry-over, and session consumption.
/// </summary>
public interface IBalanceService
{
    /// <summary>
    /// Returns the current usable package and total remaining sessions for a user.
    /// </summary>
    Task<CurrentBalanceResponse> GetCurrentBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BalanceHistoryResponse>> GetBalanceHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserTrainingBalanceResponse>> GetUserBalancesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a monthly 12-session package and applies Package12 carry-over rules.
    /// </summary>
    Task<UserTrainingBalanceResponse> CreatePackage12Async(
        Guid userId,
        CreatePackage12Request request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a monthly 6-session package.
    /// </summary>
    Task<UserTrainingBalanceResponse> CreatePackage6Async(
        Guid userId,
        CreatePackage6Request request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or extends a non-expiring single-session balance.
    /// </summary>
    Task<UserTrainingBalanceResponse> AddSingleSessionsAsync(
        Guid userId,
        AddSingleSessionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the unused-session carry-over from the immediately previous Package12 balance.
    /// </summary>
    Task ApplyCarryOverAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes one session using monthly-package priority before single sessions.
    /// </summary>
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
