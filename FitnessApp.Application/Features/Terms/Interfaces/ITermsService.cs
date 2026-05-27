using FitnessApp.Application.Features.Terms.DTOs;

namespace FitnessApp.Application.Features.Terms.Interfaces;

public interface ITermsService
{
    Task<TermsResponse> GetTermsAsync(CancellationToken cancellationToken = default);

    Task<TermsResponse> UpdateTermsAsync(
        UpdateTermsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
