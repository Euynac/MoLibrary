namespace Monica.Configuration.Models;

/// <summary>
/// Describes the active sensitive-value protection key for diagnostics.
/// </summary>
public sealed record ConfigurationProtectionKeyDescriptor
{
    /// <summary>
    /// Gets the key id.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the creation time.
    /// </summary>
    public DateTimeOffset CreatedTime { get; init; }

    /// <summary>
    /// Gets the expiration time.
    /// </summary>
    public DateTimeOffset? ExpiresTime { get; init; }
}
