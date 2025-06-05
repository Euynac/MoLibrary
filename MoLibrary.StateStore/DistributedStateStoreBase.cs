using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;

namespace MoLibrary.StateStore;

public abstract class DistributedStateStoreBase(ILogger logger) : StateStoreBase(logger), IDistributedStateStore
{
    public abstract Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default) where T : class;
    public async Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        return await GetBulkStateAsync(keys, null, removePrefix, removeEmptyValue, cancellationToken);
    }
    public abstract Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    

    public async Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync(key, null, cancellationToken);
    }

    public abstract Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default);
}