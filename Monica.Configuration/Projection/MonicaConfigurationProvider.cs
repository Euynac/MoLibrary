using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Projection;

/// <summary>
/// Single Microsoft.Extensions.Configuration provider that exposes Monica effective value documents.
/// </summary>
internal sealed class MonicaConfigurationProvider(MonicaConfigurationProviderAccessor accessor) : ConfigurationProvider
{
    /// <inheritdoc />
    public override void Load()
    {
        ReloadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reloads effective value documents and emits a new flat configuration projection.
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
        var effectiveValueStore = accessor.ServiceProvider.GetRequiredService<IConfigurationEffectiveValueStore>();
        var seedFactory = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueSeedFactory>();
        var documentEditor = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueDocumentEditor>();
        var metricsRecorder = accessor.ServiceProvider.GetRequiredService<ConfigurationMetricsRecorder>();
        var stateTracker = accessor.ServiceProvider.GetRequiredService<IConfigurationStoreStateTracker>();

        try
        {
            var projected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var definitions = definitionRegistry.GetAll();
            var seeds = definitions
                .Select(definition => new ConfigurationEffectiveValueSeed
                {
                    Definition = definition,
                    SeedJson = seedFactory.CreateSeedJson(definition)
                })
                .ToArray();
            var documents = await effectiveValueStore.EnsureCreatedAsync(seeds, cancellationToken);

            foreach (var (definition, document) in definitions.Zip(documents))
            {
                foreach (var (key, value) in documentEditor.Project(definition, document.Json))
                {
                    projected[key] = value;
                }
            }

            Data = projected;
            stateTracker.RecordSuccess(effectiveValueStore.Descriptor.StoreKey);
            metricsRecorder.RecordReloadLatency(TimeProvider.System.GetElapsedTime(started));
            OnReload();
        }
        catch (Exception ex)
        {
            stateTracker.RecordFailure(effectiveValueStore.Descriptor.StoreKey, ex);
            throw;
        }
    }
}
