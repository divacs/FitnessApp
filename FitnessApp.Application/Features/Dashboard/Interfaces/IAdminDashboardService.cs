using FitnessApp.Application.Features.Dashboard.DTOs;

namespace FitnessApp.Application.Features.Dashboard.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
