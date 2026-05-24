namespace FitnessApp.Domain.Entities;

/// <summary>
/// Represents a configurable application setting.
/// </summary>
public class AppSetting
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
