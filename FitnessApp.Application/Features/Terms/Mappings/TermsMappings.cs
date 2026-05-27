using FitnessApp.Application.Features.Terms.DTOs;
using FitnessApp.Domain.Entities;

namespace FitnessApp.Application.Features.Terms.Mappings;

public static class TermsMappings
{
    public static TermsResponse ToResponse(this TermsPage termsPage)
    {
        return new TermsResponse
        {
            Id = termsPage.Id,
            Content = termsPage.Content,
            UpdatedAt = termsPage.UpdatedAt,
            UpdatedByAdminId = termsPage.UpdatedByAdminId
        };
    }
}
