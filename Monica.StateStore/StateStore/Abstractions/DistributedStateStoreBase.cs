using Microsoft.Extensions.Logging;
using Monica.StateStore.StateStore.Queries;

namespace Monica.StateStore.StateStore.Abstractions;

/// <summary>
/// Abstract base class for distributed state store implementations
/// </summary>
public abstract class DistributedStateStoreBase(ILogger logger) : StateStoreBase(logger), IDistributedStateStore
{
    public abstract Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default) where T : class;

    public abstract Task<Dictionary<string, string>> GetRawBulkStateAsync(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    public abstract Task<string?> GetRawStateAsync(string key, CancellationToken cancellationToken = default);
}
