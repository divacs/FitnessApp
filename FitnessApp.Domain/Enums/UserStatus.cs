namespace FitnessApp.Domain.Enums;

/// <summary>
/// Defines whether a user can access and use the system.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User is registered but not verified by an administrator.
    /// </summary>
    Unverified = 1,

    /// <summary>
    /// User is verified and allowed to use the system.
    /// </summary>
    Verified = 2,

    /// <summary>
    /// User is blocked and cannot use the system.
    /// </summary>
    Blocked = 3
}
