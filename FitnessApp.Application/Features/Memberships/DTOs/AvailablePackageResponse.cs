using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Memberships.DTOs;

public class AvailablePackageResponse
{
    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string LearnMore { get; init; } = string.Empty;

    public string LearnMoreDescription { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Details { get; init; } = System.Array.Empty<string>();

    public string? Badge { get; init; }

    public IReadOnlyCollection<string> Features { get; init; } = System.Array.Empty<string>();

    public string Price { get; init; } = string.Empty;

    public PurchaseType PurchaseType { get; init; }

    public int NumberOfSessions { get; init; }
}
