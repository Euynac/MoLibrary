using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Tracks runtime health for the selected configuration stores in the current process.
/// </summary>
public interface IConfigurationStoreStateTracker
{
    /// <summary>
    /// Records a successful store operation.
    /// </summary>
    /// <param name="storeKey">The store key.</param>
    void RecordSuccess(string storeKey);

    /// <summary>
    /// Records a failed store operation.
    /// </summary>
    /// <param name="storeKey">The store key.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    void RecordFailure(string storeKey, Exception exception);

    /// <summary>
    /// Gets the latest known store states.
    /// </summary>
    IReadOnlyList<ConfigurationStoreState> GetStates();
}
