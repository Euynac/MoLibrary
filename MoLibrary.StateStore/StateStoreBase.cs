using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore;

/// <summary>
/// Abstract base class for state store implementations
/// </summary>
public abstract class StateStoreBase(ILogger logger) : IMoStateStore
{
    protected readonly ILogger Logger = logger;

    public virtual async Task<bool> ExistAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<object>(key, cancellationToken) != null;
    }

    public abstract Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default);

    public abstract Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    public abstract Task DeleteStateAsync(string key, CancellationToken cancellationToken = default);

    public abstract Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    public abstract Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

    public abstract Task<(T? Value, string ETag)> GetStateAndVersionAsync<T>(string key, CancellationToken cancellationToken = default);

    public abstract Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    public abstract Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    public abstract Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default);
}
