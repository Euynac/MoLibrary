using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

internal sealed class ConfigurationUnifiedVersionCategoryFilter(IReadOnlySet<string> categories)
    : IConfigurationUnifiedVersionFilter
{
    public bool ShouldInclude(ConfigurationDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.Category)
               && categories.Contains(definition.Category);
    }
}
