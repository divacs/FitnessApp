using FitnessApp.Application.Features.Dashboard.DTOs;

namespace FitnessApp.Application.Features.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<UserDashboardResponse> GetUserDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
