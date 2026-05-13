namespace Monica.Configuration.Models;

/// <summary>
/// Captures runtime health information for one configuration source.
/// </summary>
public sealed record ConfigurationSourceState
{
    /// <summary>
    /// Gets the source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the last reload time.
    /// </summary>
    public DateTimeOffset? LastReloadTime { get; init; }

    /// <summary>
    /// Gets whether the last reload succeeded.
    /// </summary>
    public bool LastReloadSucceeded { get; init; }

    /// <summary>
    /// Gets the last error message.
    /// </summary>
    public string? LastError { get; init; }
}
