using Microsoft.Extensions.Configuration;

namespace MoLibrary.Core.Extensions;

public static class MicrosoftConfigurationExtensions
{
    public static IEnumerable<(string Key, string? Value)> GetSectionRecursively(this IConfiguration configuration, string sectionName)
    {
        var section = configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            yield break;
        }

        foreach (var child in section.GetChildren())
        {
            yield return (child.Path, child.Value);

            foreach (var nestedChild in GetSectionRecursively(configuration, child.Path))
            {
                yield return nestedChild;
            }
        }
    }
}