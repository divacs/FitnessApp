using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Memberships.DTOs;

public class AvailablePackageResponse
{
    public PurchaseType PurchaseType { get; init; }

    public int NumberOfSessions { get; init; }
}
