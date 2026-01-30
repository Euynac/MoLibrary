using Microsoft.Extensions.Logging;
using Monica.StateStore.QueryBuilder;
using Monica.StateStore.QueryBuilder.Interfaces;

namespace Monica.StateStore;

/// <summary>
/// Abstract base class for distributed state store implementations
/// </summary>
public abstract class DistributedStateStoreBase(ILogger logger) : StateStoreBase(logger), IDistributedStateStore
{
    public abstract Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default) where T : class;

    public abstract Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    public abstract Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default);
}
