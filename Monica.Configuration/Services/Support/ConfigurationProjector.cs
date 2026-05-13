using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Projects merged values into flat Microsoft configuration key/value pairs.
/// </summary>
internal sealed class ConfigurationProjector(
    ConfigurationPathProjector pathProjector,
    ConfigurationStoredValueCodec codec)
    : IConfigurationProjector
{
    /// <inheritdoc />
    public IReadOnlyList<ProjectedConfigurationKey> Project(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IReadOnlyList<MergedNodeValue> values)
    {
        var definitionsByKey = definitions.ToDictionary(x => x.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        return values
            .Where(value => definitionsByKey.ContainsKey(value.DefinitionKey))
            .Select(value =>
            {
                var definition = definitionsByKey[value.DefinitionKey];
                return new ProjectedConfigurationKey
                {
                    Key = value.Override.ConfigurationPath ?? pathProjector.Project(definition.SectionPath, value.LogicalPath),
                    Value = codec.ToConfigurationString(value.Override.Value)
                };
            })
            .ToArray();
    }
}
