using Monica.StateStore.StateStore.Models;

namespace Monica.StateStore.UI.Models;

/// <summary>
/// Represents one state store key in the dashboard workspace.
/// </summary>
public class StateStoreKeyInfo
{
    /// <summary>
    /// Key name.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Raw value preview shown by the dashboard.
    /// </summary>
    public string? RawValue { get; set; }

    /// <summary>
    /// Whether the value preview has been loaded.
    /// </summary>
    public bool IsValueLoaded { get; set; }

    /// <summary>
    /// ETag used for optimistic concurrency.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Remaining TTL when the provider exposes it.
    /// </summary>
    public TimeSpan? TTL { get; set; }

    /// <summary>
    /// TTL availability for the current provider and key.
    /// </summary>
    public EStateStoreKeyTtlStatus TTLStatus { get; set; } = EStateStoreKeyTtlStatus.Unknown;

    /// <summary>
    /// Error message captured while loading the value preview.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Create or update request model for a state store key.
/// </summary>
public class StateStoreKeyUpdateRequest
{
    /// <summary>
    /// Key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// JSON payload or raw string payload.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// ETag used for optimistic concurrency.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Time to live.
    /// </summary>
    public TimeSpan? TTL { get; set; }
}

/// <summary>
/// Legacy key scan result kept for compatibility with existing components.
/// </summary>
public record KeyScanResult
{
    /// <summary>
    /// Matched keys.
    /// </summary>
    public List<string> Keys { get; init; } = [];

    /// <summary>
    /// Total matched count.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Whether more results exist after the current page.
    /// </summary>
    public bool HasMore { get; init; }
}
