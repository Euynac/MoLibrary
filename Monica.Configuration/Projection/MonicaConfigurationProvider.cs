using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;

namespace Monica.Configuration.Projection;

/// <summary>
/// Single Microsoft.Extensions.Configuration provider that exposes the merged Monica configuration projection.
/// </summary>
internal sealed class MonicaConfigurationProvider(MonicaConfigurationProviderAccessor accessor) : ConfigurationProvider
{
    /// <inheritdoc />
    public override void Load()
    {
        ReloadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reloads source overrides and emits a new flat configuration projection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (accessor.ServiceProvider is null)
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        var definitionRegistry = accessor.ServiceProvider.GetRequiredService<IConfigurationDefinitionRegistry>();
        var aggregator = accessor.ServiceProvider.GetRequiredService<IConfigurationOverrideAggregator>();
        var mergeEngine = accessor.ServiceProvider.GetRequiredService<IConfigurationMergeEngine>();
        var projector = accessor.ServiceProvider.GetRequiredService<IConfigurationProjector>();
        var metricsRecorder = accessor.ServiceProvider.GetRequiredService<ConfigurationMetricsRecorder>();

        var sourceSets = await aggregator.LoadAsync(cancellationToken);
        var merged = mergeEngine.Merge(sourceSets);
        var projected = projector.Project(definitionRegistry.GetAll(), merged);
        Data = projected.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        metricsRecorder.RecordReloadLatency(TimeProvider.System.GetElapsedTime(started));
        OnReload();
    }
}
