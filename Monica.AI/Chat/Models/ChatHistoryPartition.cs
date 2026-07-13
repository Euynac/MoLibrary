namespace Monica.AI.Chat.Models;

/// <summary>
/// Identifies the isolated storage partition used for chat history operations.
/// </summary>
/// <remarks>
/// Browser providers typically resolve this key from a stable browser installation identifier.
/// Server providers should resolve it from authenticated tenant and user identity rather than
/// accepting a caller-supplied value.
/// </remarks>
public sealed record ChatHistoryPartition
{
    /// <summary>Current serialized contract version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Creates a validated history partition.</summary>
    /// <param name="key">Provider-specific opaque partition key.</param>
    public ChatHistoryPartition(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Contract version used to create this value.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Provider-specific opaque partition key.</summary>
    public string Key { get; }
}
