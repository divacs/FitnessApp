namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents the editable terms page content.
/// </summary>
public class TermsPage
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByAdminId { get; set; }

    public ApplicationUser? UpdatedByAdmin { get; set; }
}
