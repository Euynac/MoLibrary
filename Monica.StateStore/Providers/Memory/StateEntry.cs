namespace Monica.StateStore.Providers.Memory;

/// <summary>
/// Non-generic interface to memory state storage entries for type-independent access
/// </summary>
public interface IStateEntry
{
    /// <summary>
    /// Status data (accessed as object)
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// ETag
    /// </summary>
    string ETag { get; }

    /// <summary>
    /// creation time
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Last updated
    /// </summary>
    DateTimeOffset UpdatedAt { get; }

    DateTimeOffset? ExpiresAt { get; }
}

/// <summary>
/// Memory state storage entries
/// </summary>
/// <typeparam name="T">Status data type</typeparam>
public class StateEntry<T> : IStateEntry
{
    /// <summary>
    /// status data
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// IStateEntry explicit interface implementation, returning generic values ​​as object
    /// </summary>
    object? IStateEntry.Value => Value;
    
    /// <summary>
    /// Version number, starting from 0 and incrementing with each update
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// ETag, generated based on version number
    /// </summary>
    public string ETag => Version.ToString();
    
    /// <summary>
    /// creation time
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// Last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public StateEntry()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public StateEntry(T? value, int version = 0, DateTimeOffset? expiresAt = null) : this()
    {
        Value = value;
        Version = version;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Updates the value, version, and expiration metadata.
    /// </summary>
    /// <param name="newValue">Updated state value.</param>
    /// <param name="expiresAt">Absolute expiration time, if any.</param>
    public void Update(T? newValue, DateTimeOffset? expiresAt = null)
    {
        Value = newValue;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
    }
} 
