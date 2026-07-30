using System.Collections.Concurrent;
using System.Text.Json;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Queries;

namespace Monica.Testing.Doubles;

/// <summary>
/// In-memory distributed state store for sociable tests that should not contact external state infrastructure.
/// </summary>
public sealed class InMemoryDistributedStateStore : IDistributedStateStore
{
    private readonly ConcurrentDictionary<string, StoredState> _states = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<bool> ExistAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.ContainsKey(key));
    }

    /// <inheritdoc />
    public Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.TryGetValue(key, out var state)
            ? JsonSerializer.Deserialize<T>(state.RawJson, _jsonOptions)
            : default);
    }

    /// <inheritdoc />
    public Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        _states[key] = StoredState.Create(value, _jsonOptions);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(
        IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>();
        foreach (var key in keys)
        {
            var value = await GetStateAsync<T>(key, cancellationToken);
            if (!removeEmptyValue || value is not null)
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _states.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(T? Value, string ETag)> GetStateAndETagAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetStateAsync<T>(key, cancellationToken);
        var etag = _states.TryGetValue(key, out var state) ? state.ETag : string.Empty;
        return (value, etag);
    }

    /// <inheritdoc />
    public Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(
        string key,
        T value,
        string expectedETag,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        if (!_states.TryGetValue(key, out var current) || current.ETag != expectedETag)
        {
            return Task.FromResult<(bool Success, string? NewETag)>((false, null));
        }

        var replacement = StoredState.Create(value, _jsonOptions);
        _states[key] = replacement;
        return Task.FromResult<(bool Success, string? NewETag)>((true, replacement.ETag));
    }

    /// <inheritdoc />
    public async Task<bool> TrySaveStateWithETagWithoutReadBackAsync<T>(
        string key,
        T value,
        string expectedETag,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        var (success, _) = await TrySaveStateWithETagAsync(key, value, expectedETag, cancellationToken, ttl);
        return success;
    }

    /// <inheritdoc />
    public Task<bool> TrySaveStateIfNotExistsAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        return Task.FromResult(_states.TryAdd(key, StoredState.Create(value, _jsonOptions)));
    }

    /// <inheritdoc />
    public Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var prefix = pattern.EndsWith('*') ? pattern[..^1] : pattern;
        var keys = _states.Keys
            .Where(key => pattern == "*" || key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(keys);
    }

    /// <inheritdoc />
    public Task SaveBulkStateAsync<T>(
        IReadOnlyList<(string Key, T Value)> items,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        foreach (var (key, value) in items)
        {
            _states[key] = StoredState.Create(value, _jsonOptions);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryDeleteStateWithETagAsync(string key, string expectedETag, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(key, out var current) || current.ETag != expectedETag)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_states.TryRemove(key, out _));
    }

    /// <inheritdoc />
    public Task<Dictionary<string, string>> GetRawBulkStateAsync(
        IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var key in keys)
        {
            if (_states.TryGetValue(key, out var state) || !removeEmptyValue)
            {
                result[key] = state?.RawJson ?? string.Empty;
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<Dictionary<string, T?>> QueryStateAsync<T>(
        Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default)
        where T : class
    {
        return Task.FromResult(_states.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.Deserialize<T>(pair.Value.RawJson, _jsonOptions)));
    }

    /// <inheritdoc />
    public Task<string?> GetRawStateAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.TryGetValue(key, out var state) ? state.RawJson : null);
    }

    private sealed record StoredState(string RawJson, string ETag)
    {
        public static StoredState Create<T>(T value, JsonSerializerOptions options)
        {
            return new StoredState(JsonSerializer.Serialize(value, options), Guid.NewGuid().ToString("N"));
        }
    }
}
