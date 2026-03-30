using Monica.StateStore.QueryBuilder;
using Monica.StateStore.QueryBuilder.Interfaces;

namespace Monica.StateStore;

/// <summary>
/// Distributed state store interface with additional capabilities
/// </summary>
public interface IDistributedStateStore : IMoStateStore
{
    /// <summary>
    /// Get multiple state payloads as their raw serialized text
    /// </summary>
    /// <param name="keys">State keys</param>
    /// <param name="removeEmptyValue">Whether to remove empty values from result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of key-value pairs containing the raw serialized text for each state</returns>
    Task<Dictionary<string, string>> GetRawBulkStateAsync(IReadOnlyList<string> keys,
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
    /// Get a state payload as its raw serialized text
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The raw serialized text for the state, or null when the key does not exist</returns>
    Task<string?> GetRawStateAsync(string key, CancellationToken cancellationToken = default);
}
