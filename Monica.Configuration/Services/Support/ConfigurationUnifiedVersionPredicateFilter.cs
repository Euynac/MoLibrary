using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

internal sealed class ConfigurationUnifiedVersionPredicateFilter(Func<ConfigurationDefinition, bool> predicate)
    : IConfigurationUnifiedVersionFilter
{
    public bool ShouldInclude(ConfigurationDefinition definition)
    {
        return predicate(definition);
    }
}
