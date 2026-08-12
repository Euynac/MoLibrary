using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Modules;
using Monica.Dapr.Services.Support;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Queries;

namespace Monica.Dapr.Services;

/// <summary>
/// Dapr state store implementation
/// </summary>
internal sealed class DaprStateStoreProvider(
    DaprClient dapr,
    ILogger<DaprStateStoreProvider> logger,
    IOptions<ModuleDaprStateStoreOption> options,
    IStateDocumentProfileProvider documentProfiles,
    IDaprStateQueryClient stateQueryClient) : DistributedStateStoreBase(logger)
{
    /// <summary>
    /// Configuration options
    /// </summary>
    private ModuleDaprStateStoreOption Option { get; } = options.Value;

    /// <summary>
    /// State store name
    /// </summary>
    private string StateStoreName => Option.StateStoreName;

    private StateDocumentProfile DocumentProfile { get; } =
        documentProfiles.GetRequiredProfile(options.Value.DocumentProfileName);

    public override async Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class
    {
        var queryStr = "";
        try
        {
            var queryBuilder = new QueryBuilder<T>();
            var finished = query.Invoke(queryBuilder);
            if (!ReferenceEquals(finished, queryBuilder))
            {
                throw new InvalidOperationException("State query delegates must return the supplied query builder.");
            }

            queryStr = DaprStateQueryRenderer.Render(queryBuilder.BuildDefinition(), DocumentProfile);
            var response = await stateQueryClient.QueryAsync(StateStoreName, queryStr, cancellationToken);
            return response.ToDictionary(
                static item => item.Key,
                item => item.Value is { } data ? DocumentProfile.Deserialize<T>(data) : null);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR query state from {0} using exp: {1}", StateStoreName,
                queryStr);
        }
    }

    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default) where T : default
    {
        try
        {
            return (await dapr.GetBulkStateAsync(StateStoreName, keys.ToList(), Option.DefaultBulkParallelism,
                cancellationToken: cancellationToken))
                .Where(p => !removeEmptyValue || !string.IsNullOrEmpty(p.Value))
                .ToDictionary(
                    p => p.Key,
                    item =>
                    {
                        try
                        {
                            return DocumentProfile.Deserialize<T>(item.Value);
                        }
                        catch (Exception e)
                        {
                            throw e.CreateException(Logger, "Failed to deserialize JSON value \"{0}\" to type {1}", item.Value, typeof(T).FullName);
                        }
                    });
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", keys));
        }
    }

    public override async Task<Dictionary<string, string>> GetRawBulkStateAsync(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return (await dapr.GetBulkStateAsync(StateStoreName, keys.ToList(), Option.DefaultBulkParallelism,
                cancellationToken: cancellationToken))
                .Where(p => !removeEmptyValue || !string.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, item => item.Value);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", keys));
        }
    }

    public override async Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default) where T : default
    {
        try
        {
            var data = await dapr.GetByteStateAsync(StateStoreName, key, cancellationToken: cancellationToken);
            return data.IsEmpty ? default : DocumentProfile.Deserialize<T>(data.Span);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state from {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task<string?> GetRawStateAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await dapr.GetByteStateAsync(StateStoreName, key, cancellationToken: cancellationToken);
            return data.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(data.Span);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state from {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        try
        {
            var metadata = BuildTtlMetadata(ttl);
            await dapr.SaveByteStateAsync(
                StateStoreName,
                key,
                DocumentProfile.SerializeToUtf8Bytes(value),
                metadata: metadata,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Saving state to {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await dapr.DeleteStateAsync(StateStoreName, key, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Deleting state from {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            await dapr.DeleteBulkStateAsync(StateStoreName,
                keys.Select(p => new BulkDeleteStateItem(p, string.Empty)).ToList(), cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Deleting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", keys));
        }
    }

    public override async Task<(T? Value, string ETag)> GetStateAndETagAsync<T>(string key,
        CancellationToken cancellationToken = default) where T : default
    {
        try
        {
            var (data, etag) = await dapr.GetByteStateAndETagAsync(
                StateStoreName,
                key,
                cancellationToken: cancellationToken);
            return (data.IsEmpty ? default : DocumentProfile.Deserialize<T>(data.Span), etag);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state and ETag from {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        try
        {
            var metadata = BuildTtlMetadata(ttl);

            // Use Dapr's TrySaveStateAsync for optimistic locking
            var success = await dapr.TrySaveByteStateAsync(
                StateStoreName,
                key,
                DocumentProfile.SerializeToUtf8Bytes(value),
                expectedETag,
                metadata: metadata, cancellationToken: cancellationToken);

            if (success)
            {
                // Save succeeded, get new ETag
                var (_, newETag) = await dapr.GetByteStateAndETagAsync(StateStoreName, key,
                    cancellationToken: cancellationToken);
                return (true, newETag);
            }

            Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", key, expectedETag);
            return (false, null);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TrySaveStateWithETag to {0} with key: {1}", StateStoreName, key);
        }
    }

    /// <inheritdoc />
    public override async Task<bool> TrySaveStateWithETagWithoutReadBackAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        try
        {
            var metadata = BuildTtlMetadata(ttl);
            var success = await dapr.TrySaveByteStateAsync(
                StateStoreName,
                key,
                DocumentProfile.SerializeToUtf8Bytes(value),
                expectedETag,
                metadata: metadata, cancellationToken: cancellationToken);

            if (!success)
            {
                Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", key, expectedETag);
            }

            return success;
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TrySaveStateWithETagWithoutReadBack to {0} with key: {1}", StateStoreName, key);
        }
    }

    public override async Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        try
        {
            // Check if key exists first
            var (_, existingETag) = await dapr.GetByteStateAndETagAsync(StateStoreName, key,
                cancellationToken: cancellationToken);

            // If ETag is not empty, key already exists
            if (!string.IsNullOrEmpty(existingETag))
            {
                Logger.LogDebug("Key already exists, cannot save: {Key}", key);
                return false;
            }

            // Key doesn't exist, try to save (use empty ETag to ensure it's a new key)
            var metadata = BuildTtlMetadata(ttl);

            // Use empty ETag for save, will fail if another process created this key
            var success = await dapr.TrySaveByteStateAsync(
                StateStoreName,
                key,
                DocumentProfile.SerializeToUtf8Bytes(value),
                "",
                metadata: metadata, cancellationToken: cancellationToken);

            if (success)
            {
                Logger.LogDebug("Saved state (if not exists) with key: {Key}", key);
                return true;
            }

            // Race condition: another process may have created this key
            Logger.LogDebug("Failed to save state (race condition), key: {Key}", key);
            return false;
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TrySaveStateIfNotExists to {0} with key: {1}", StateStoreName, key);
        }
    }

    public override Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Dapr state store does not support scanning keys by pattern. " +
            "Please use RedisStateStore or another implementation that supports this operation.");
    }

    public override async Task SaveBulkStateAsync<T>(
        IReadOnlyList<(string Key, T Value)> items,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        if (items.Count == 0) return;

        try
        {
            var metadata = BuildTtlMetadata(ttl);
            var serializedItems = items
                .Select(item => (item.Key, Value: DocumentProfile.SerializeToUtf8Bytes(item.Value)))
                .ToArray();

            // Dapr's typed bulk API reserializes values through the client-global JSON options. Raw byte writes keep
            // the selected durable document profile authoritative for bulk operations as well as single-item writes.
            await Task.WhenAll(serializedItems.Select(item => dapr.SaveByteStateAsync(
                StateStoreName,
                item.Key,
                item.Value,
                metadata: metadata,
                cancellationToken: cancellationToken)));
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR SaveBulkState to {0} with {1} items", StateStoreName, items.Count);
        }
    }

    public override async Task<bool> TryDeleteStateWithETagAsync(
        string key,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await dapr.TryDeleteStateAsync(StateStoreName, key, expectedETag, cancellationToken: cancellationToken);

            if (!success)
            {
                Logger.LogDebug("ETag mismatch for delete, key: {Key}. Expected: {Expected}", key, expectedETag);
            }

            return success;
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TryDeleteStateWithETag from {0} with key: {1}", StateStoreName, key);
        }
    }

    private static Dictionary<string, string>? BuildTtlMetadata(TimeSpan? ttl)
    {
        var ttlSeconds = ttl?.TotalSeconds;
        return ttlSeconds switch
        {
            < 0 => throw new InvalidOperationException("ttl can not be smaller than zero"),
            0 => new Dictionary<string, string> { { "ttlInSeconds", "-1" } },
            { } seconds => new Dictionary<string, string> { { "ttlInSeconds", ((int)seconds).ToString() } },
            _ => null
        };
    }
}
