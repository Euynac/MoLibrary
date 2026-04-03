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
