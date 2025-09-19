using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Helpers;

internal static class ConfigurationHelper
{
    /// <summary>
    /// Extracts the generator configuration from assembly-level attributes.
    /// </summary>
    /// <param name="compilation">The compilation context</param>
    /// <returns>The extracted configuration or default configuration</returns>
    public static GeneratorConfig ExtractConfiguration(Compilation compilation)
    {
        var assemblyAttributes = compilation.Assembly.GetAttributes();

        foreach (var attribute in assemblyAttributes)
        {
            if (attribute.AttributeClass?.Name == "AutoControllerGeneratorConfigAttribute")
            {
                return ParseConfigurationAttribute(attribute);
            }
        }

        return GeneratorConfig.Default;
    }

    /// <summary>
    /// Parses the configuration from an attribute data instance.
    /// </summary>
    /// <param name="attributeData">The attribute data to parse</param>
    /// <returns>The parsed configuration</returns>
    private static GeneratorConfig ParseConfigurationAttribute(AttributeData attributeData)
    {
        var config = new GeneratorConfig();

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "DefaultRoutePrefix":
                    config.DefaultRoutePrefix = namedArgument.Value.Value?.ToString();
                    break;
                case "DomainName":
                    config.DomainName = namedArgument.Value.Value?.ToString();
                    break;
                case "RequireExplicitRoutes":
                    if (namedArgument.Value.Value is bool requireExplicit)
                        config.RequireExplicitRoutes = requireExplicit;
                    break;
            }
        }

        return config;
    }
}