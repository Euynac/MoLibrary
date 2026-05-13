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
    ConfigurationStoredValueCodec codec,
    IConfigurationSensitiveValueProtector sensitiveValueProtector)
    : IConfigurationSourceChainService
{
    /// <inheritdoc />
    public async Task<ConfigurationSourceChain> GetSourceChainAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var definition = definitions.GetRequired(definitionKey);
        var targetNode = ResolveTargetNode(definition, logicalPath);
        var isSensitive = targetNode?.IsSensitive is true;
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
                HasValue = value?.State == ConfigurationValueState.Active,
                IsSensitive = isSensitive,
                DisplayValue = value is null || isSensitive
                    ? null
                    : value.Granularity == ConfigurationOverrideGranularity.Container
                        ? value.Value.PlainJson
                        : codec.ToConfigurationString(sensitiveValueProtector.Unprotect(value.Value)),
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

    private static ConfigurationNodeDefinition? ResolveTargetNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }
}
