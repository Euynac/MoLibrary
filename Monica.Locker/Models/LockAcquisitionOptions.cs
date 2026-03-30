namespace Monica.Locker.Models;

/// <summary>
/// Describes how a lock should be acquired.
/// </summary>
/// <remarks>
/// Any <see langword="null"/> value allows the locker module or the active provider to apply its default behavior.
/// </remarks>
public sealed class LockAcquisitionOptions
{
    /// <summary>
    /// Optional logical owner identifier recorded by providers that support ownership metadata.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Maximum time to wait for a lock before returning <see langword="null"/>.
    /// </summary>
    public TimeSpan? WaitTimeout { get; init; }

    /// <summary>
    /// Requested lease duration for providers that support expiring locks automatically.
    /// </summary>
    public TimeSpan? LeaseDuration { get; init; }
}
