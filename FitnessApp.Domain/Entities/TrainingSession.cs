namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a scheduled group training session.
/// </summary>
public class TrainingSession
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; }

    public string TrainerName { get; set; } = "Sara";

    public string Location { get; set; } = string.Empty;

    public bool IsCancelled { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
