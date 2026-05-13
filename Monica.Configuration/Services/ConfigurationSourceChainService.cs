using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Builds source-chain diagnostics from registered sources.
/// </summary>
internal sealed class ConfigurationSourceChainService(
    IConfigurationDefinitionRegistry definitions,
    IEnumerable<IConfigurationValueSource> sources,
    ConfigurationStoredValueCodec codec)
    : IConfigurationSourceChainService
{
    /// <inheritdoc />
    public async Task<ConfigurationSourceChain> GetSourceChainAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var definition = definitions.GetRequired(definitionKey);
        var sourceValues = new List<ConfigurationSourceValue>();
        ConfigurationValueOverride? effective = null;

        foreach (var source in sources.OrderByDescending(x => x.Descriptor.Priority))
        {
            var value = await source.GetAsync(definitionKey, logicalPath, cancellationToken);
            if (effective is null && value?.State == ConfigurationValueState.Active)
            {
                effective = value;
            }

            sourceValues.Add(new ConfigurationSourceValue
            {
                SourceKey = source.Descriptor.SourceKey,
                DisplayName = source.Descriptor.DisplayName,
                Kind = source.Descriptor.Kind,
                Priority = source.Descriptor.Priority,
                HasValue = value is not null,
                IsSensitive = false,
                DisplayValue = value is null ? null : codec.ToConfigurationString(value.Value),
                LastModifiedTime = value?.LastModifiedTime
            });
        }

        return new ConfigurationSourceChain
        {
            DefinitionKey = definitionKey,
            LogicalPath = logicalPath,
            ConfigurationPath = effective?.ConfigurationPath,
            Sources = sourceValues,
            EffectiveSourceKey = effective?.SourceKey
        };
    }
}
