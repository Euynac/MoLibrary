using Monica.Configuration.Annotations;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Resolves the root Microsoft configuration section for Monica-managed options types.
/// </summary>
internal static class ConfigurationSectionPathResolver
{
    public static string Resolve(
        Type optionsType,
        ConfigurationAttribute attribute,
        ConfigurationSectionPathConvention convention)
    {
        if (!string.IsNullOrWhiteSpace(attribute.SectionPath))
        {
            return attribute.SectionPath;
        }

        return convention switch
        {
            ConfigurationSectionPathConvention.ShortTypeName => optionsType.Name,
            ConfigurationSectionPathConvention.ClrFullName => (optionsType.FullName ?? optionsType.Name).Replace('.', ':'),
            _ => throw new ArgumentOutOfRangeException(nameof(convention), convention, "Unsupported configuration section path convention.")
        };
    }
}
