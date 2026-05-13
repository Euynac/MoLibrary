using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// In-memory runtime state tracker for configuration sources.
/// </summary>
internal sealed class ConfigurationSourceStateTracker : IConfigurationSourceStateTracker
{
    private readonly ConcurrentDictionary<string, ConfigurationSourceState> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void RecordReload(string sourceKey, Exception? error = null)
    {
        _states[sourceKey] = new ConfigurationSourceState
        {
            SourceKey = sourceKey,
            LastReloadTime = DateTimeOffset.UtcNow,
            LastReloadSucceeded = error is null,
            LastError = error?.Message
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ConfigurationSourceState> GetStates()
    {
        return [.. _states.Values.OrderBy(x => x.SourceKey, StringComparer.OrdinalIgnoreCase)];
    }
}
