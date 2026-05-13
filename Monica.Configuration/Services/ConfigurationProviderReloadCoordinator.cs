using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;
using Monica.Configuration.Projection;

namespace Monica.Configuration.Services;

/// <summary>
/// Coordinates reloads for the active Monica configuration provider.
/// </summary>
internal sealed class ConfigurationProviderReloadCoordinator(
    MonicaConfigurationProviderAccessor accessor,
    ConfigurationMetricsRecorder metricsRecorder)
    : IConfigurationReloadCoordinator
{
    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (accessor.Provider is null)
        {
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        await accessor.Provider.ReloadAsync(cancellationToken);
        metricsRecorder.RecordReloadLatency(TimeProvider.System.GetElapsedTime(started));
    }
}
