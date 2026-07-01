using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

internal sealed class ConfigurationUnifiedVersionDefinitionKeyFilter(IReadOnlySet<string> definitionKeys)
    : IConfigurationUnifiedVersionFilter
{
    public bool ShouldInclude(ConfigurationDefinition definition)
    {
        return definitionKeys.Contains(definition.DefinitionKey);
    }
}
