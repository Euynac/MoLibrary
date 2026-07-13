namespace Monica.UI.Shell.Support;

/// <summary>
/// Browser storage type
/// </summary>
public enum BrowserStorageType
{
    Local,
    Session
}

/// <summary>
/// Describes why a browser-storage write could not be completed.
/// </summary>
public enum BrowserStorageWriteFailureKind
{
    /// <summary>
    /// The write completed successfully.
    /// </summary>
    None,

    /// <summary>
    /// The browser rejected the write because the selected storage area exceeded its quota.
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// The value could not be serialized to JSON.
    /// </summary>
    SerializationFailed,

    /// <summary>
    /// Browser storage or the active Blazor circuit was unavailable.
    /// </summary>
    StorageUnavailable,

    /// <summary>
    /// The write failed for a reason that could not be classified.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents the observable outcome of a browser-storage write.
/// </summary>
/// <param name="FailureKind">The classified failure, or <see cref="BrowserStorageWriteFailureKind.None"/> on success.</param>
public sealed record BrowserStorageWriteResult(BrowserStorageWriteFailureKind FailureKind)
{
    /// <summary>
    /// Gets a value indicating whether the write completed successfully.
    /// </summary>
    public bool Succeeded => FailureKind == BrowserStorageWriteFailureKind.None;
}

/// <summary>
/// Generic browser storage service for persisting UI state in localStorage/sessionStorage.
/// All keys are auto-prefixed with "mo:" to avoid collisions.
/// </summary>
public interface IBrowserStorage : IAsyncDisposable
{
    /// <summary>
    /// Get a value from browser storage, deserialized from JSON
    /// </summary>
    Task<T> GetAsync<T>(string key, T defaultValue, BrowserStorageType storageType = BrowserStorageType.Local);

    /// <summary>
    /// Set a value in browser storage, serialized as JSON
    /// </summary>
    Task SetAsync<T>(string key, T value, BrowserStorageType storageType = BrowserStorageType.Local);

    /// <summary>
    /// Attempts to set a JSON-serialized value and returns an observable result instead of silently discarding failures.
    /// </summary>
    /// <remarks>
    /// Browser quota failures are reported when the browser exposes a recognizable quota exception. Serialization,
    /// unavailable-storage, and unclassified failures are returned as result values rather than thrown.
    /// </remarks>
    Task<BrowserStorageWriteResult> TrySetAsync<T>(string key, T value, BrowserStorageType storageType = BrowserStorageType.Local);

    /// <summary>
    /// Remove a value from browser storage
    /// </summary>
    Task RemoveAsync(string key, BrowserStorageType storageType = BrowserStorageType.Local);

    /// <summary>
    /// Get all keys matching a category prefix (e.g. "table" matches "mo:table:*")
    /// </summary>
    Task<IReadOnlyList<string>> GetKeysAsync(string category, BrowserStorageType storageType = BrowserStorageType.Local);

    /// <summary>
    /// Clear all keys matching a category prefix. Returns the number of items removed.
    /// </summary>
    Task<int> ClearCategoryAsync(string category, BrowserStorageType storageType = BrowserStorageType.Local);
}
