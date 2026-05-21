namespace Monica.Configuration.Models;

/// <summary>
/// Payload sent through distributed configuration change notification transports.
/// </summary>
public sealed record ConfigurationChangeNotification
{
    /// <summary>
    /// Gets the unique notification id.
    /// </summary>
    public required string NotificationId { get; init; }

    /// <summary>
    /// Gets the definition key affected by the change.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the changed logical path.
    /// </summary>
    public LogicalPath? LogicalPath { get; init; }

    /// <summary>
    /// Gets the new version.
    /// </summary>
    public long? Version { get; init; }

    /// <summary>
    /// Gets when the change occurred.
    /// </summary>
    public DateTimeOffset ChangedTime { get; init; } = DateTimeOffset.UtcNow;
}
