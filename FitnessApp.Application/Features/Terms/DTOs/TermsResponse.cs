namespace FitnessApp.Application.Features.Terms.DTOs;

public class TermsResponse
{
    public Guid? Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedByAdminId { get; set; }
}
