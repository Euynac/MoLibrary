using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Loads and normalizes overrides from all registered sources.
/// </summary>
internal sealed class ConfigurationOverrideAggregator(
    IEnumerable<IConfigurationValueSource> sources,
    IConfigurationOverrideNormalizer normalizer,
    IConfigurationSourceStateTracker stateTracker)
    : IConfigurationOverrideAggregator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NormalizedOverrideSet>> LoadAsync(CancellationToken cancellationToken)
    {
        var result = new List<NormalizedOverrideSet>();
        foreach (var source in sources)
        {
            try
            {
                var overrides = await source.LoadAsync(cancellationToken);
                result.Add(new NormalizedOverrideSet
                {
                    Source = source.Descriptor,
                    Overrides = normalizer.Normalize(overrides)
                });
                stateTracker.RecordReload(source.Descriptor.SourceKey);
            }
            catch (Exception ex)
            {
                stateTracker.RecordReload(source.Descriptor.SourceKey, ex);
                throw;
            }
        }

        return result;
    }
}
