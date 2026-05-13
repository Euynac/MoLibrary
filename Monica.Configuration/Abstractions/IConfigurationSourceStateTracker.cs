using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Tracks runtime state of configuration value sources.
/// </summary>
public interface IConfigurationSourceStateTracker
{
    /// <summary>
    /// Records a source reload result.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <param name="error">The reload error, if any.</param>
    void RecordReload(string sourceKey, Exception? error = null);

    /// <summary>
    /// Gets all source states.
    /// </summary>
    /// <returns>The source states.</returns>
    IReadOnlyList<ConfigurationSourceState> GetStates();
}
