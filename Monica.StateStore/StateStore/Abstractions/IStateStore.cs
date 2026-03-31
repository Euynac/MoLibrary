namespace Monica.StateStore.StateStore.Abstractions;

/// <summary>
/// Core state store interface - pure key-value operations without prefix concept
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Check if a key exists
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get state data by key
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State data or null if not found</returns>
    Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save state data
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="key">State key</param>
    /// <param name="value">State value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="ttl">Time to live. Use TimeSpan.Zero for permanent storage</param>
    Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// Delete state by key
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteStateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get multiple states by keys
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="keys">State keys</param>
    /// <param name="removeEmptyValue">Whether to remove empty values from result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of key-value pairs</returns>
    Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple states by keys
    /// </summary>
    /// <param name="keys">State keys</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get state data with ETag for optimistic concurrency
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of value and ETag</returns>
    Task<(T? Value, string ETag)> GetStateAndETagAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save state with ETag verification (optimistic locking)
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="key">State key</param>
    /// <param name="value">State value</param>
    /// <param name="expectedETag">Expected ETag value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="ttl">Time to live</param>
    /// <returns>Tuple of success flag and new ETag</returns>
    Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// Save state only if key does not exist (SetIfNotExists)
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="key">State key</param>
    /// <param name="value">State value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="ttl">Time to live</param>
    /// <returns>True if saved (key didn't exist), false if key already exists</returns>
    Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// Scan keys matching a glob pattern
    /// </summary>
    /// <param name="pattern">Glob pattern (e.g., "user:*", "session:*:data")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching keys</returns>
    Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save multiple states in bulk
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="items">List of key-value pairs to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="ttl">Time to live for all items</param>
    Task SaveBulkStateAsync<T>(IReadOnlyList<(string Key, T Value)> items,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null);

    /// <summary>
    /// Delete state with ETag verification (optimistic locking)
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="expectedETag">Expected ETag value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false if ETag mismatch</returns>
    Task<bool> TryDeleteStateWithETagAsync(string key, string expectedETag,
        CancellationToken cancellationToken = default);
}
