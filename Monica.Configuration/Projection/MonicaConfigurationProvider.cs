using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;

namespace Monica.Configuration.Projection;

/// <summary>
/// Single Microsoft.Extensions.Configuration provider that exposes the merged Monica configuration projection.
/// </summary>
internal sealed class MonicaConfigurationProvider(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationOverrideAggregator aggregator,
    IConfigurationMergeEngine mergeEngine,
    IConfigurationProjector projector)
    : ConfigurationProvider
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
        var sourceSets = await aggregator.LoadAsync(cancellationToken);
        var merged = mergeEngine.Merge(sourceSets);
        var projected = projector.Project(definitionRegistry.GetAll(), merged);
        Data = projected.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        OnReload();
    }
}
