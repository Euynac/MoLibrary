using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// In-process runtime state tracker for selected configuration stores.
/// </summary>
internal sealed class ConfigurationStoreStateTracker : IConfigurationStoreStateTracker
{
    private readonly ConcurrentDictionary<string, ConfigurationStoreState> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void RecordSuccess(string storeKey)
    {
        _states[storeKey] = new ConfigurationStoreState
        {
            StoreKey = storeKey,
            LastOperationTime = DateTimeOffset.UtcNow,
            LastOperationSucceeded = true
        };
    }

    /// <inheritdoc />
    public void RecordFailure(string storeKey, Exception exception)
    {
        _states[storeKey] = new ConfigurationStoreState
        {
            StoreKey = storeKey,
            LastOperationTime = DateTimeOffset.UtcNow,
            LastOperationSucceeded = false,
            LastError = exception.Message
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ConfigurationStoreState> GetStates()
    {
        return [.. _states.Values.OrderBy(state => state.StoreKey, StringComparer.OrdinalIgnoreCase)];
    }
}
