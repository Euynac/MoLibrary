using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;

namespace MoLibrary.StateStore;

/// <summary>
/// Distributed state store interface with additional capabilities
/// </summary>
public interface IDistributedStateStore : IMoStateStore
{
    /// <summary>
    /// Get multiple states as raw strings
    /// </summary>
    /// <param name="keys">State keys</param>
    /// <param name="removeEmptyValue">Whether to remove empty values from result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of key-value pairs as strings</returns>
    Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query states matching given conditions (supported by backends like Dapr with queryable stores)
    /// </summary>
    /// <typeparam name="T">State data type</typeparam>
    /// <param name="query">Query builder function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of matching states</returns>
    Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Get single state as raw string
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State data as string or null</returns>
    Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default);
}
