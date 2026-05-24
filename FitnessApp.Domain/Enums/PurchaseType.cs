namespace FitnessApp.Domain.Enums;

/// <summary>
/// Defines the type of training purchase made by a user.
/// </summary>
public enum PurchaseType
{
    /// <summary>
    /// Package with twelve training sessions.
    /// </summary>
    Package12 = 1,

    /// <summary>
    /// Package with six training sessions.
    /// </summary>
    Package6 = 2,

    /// <summary>
    /// Purchase of individual training sessions.
    /// </summary>
    SingleSessions = 3
}
