using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Diagnostics;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Helpers;

internal static class ConfigurationHelper
{
    /// <summary>
    /// Extracts the generator configuration from assembly-level attributes without error reporting.
    /// Used in incremental generator pipelines where SourceProductionContext is not available.
    /// </summary>
    /// <param name="compilation">The compilation context</param>
    /// <returns>The extracted configuration, error message, or null for success with default config</returns>
    public static (GeneratorConfig? Config, string? ErrorMessage) ExtractConfiguration(Compilation compilation)
    {
        try
        {
            var assemblyAttributes = compilation.Assembly.GetAttributes();
            AttributeData? configAttribute = null;

            foreach (var attribute in assemblyAttributes)
            {
                if (attribute.AttributeClass?.Name == "AutoControllerGeneratorConfigAttribute")
                {
                    configAttribute = attribute;
                    break;
                }
            }

            if (configAttribute == null)
            {
                // No configuration found - this might be intentional if RequireExplicitRoutes is the default behavior
                return (GeneratorConfig.Default, null);
            }

            var (config, errorMessage) = ParseConfigurationAttribute(configAttribute);
            return (config, errorMessage);
        }
        catch (Exception ex)
        {
            return (null, $"Configuration extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the generator configuration from assembly-level attributes with error reporting.
    /// </summary>
    /// <param name="compilation">The compilation context</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>The extracted configuration or null if extraction failed</returns>
    public static GeneratorConfig? ExtractConfiguration(Compilation compilation, SourceProductionContext context)
    {
        var (config, errorMessage) = ExtractConfiguration(compilation);

        if (errorMessage != null)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConfigurationExtractionFailed,
                Location.None,
                errorMessage);
            context.ReportDiagnostic(diagnostic);
            return null;
        }

        return config;
    }


    /// <summary>
    /// Parses the configuration from an attribute data instance without error reporting.
    /// Used in incremental generator pipelines where SourceProductionContext is not available.
    /// </summary>
    /// <param name="attributeData">The attribute data to parse</param>
    /// <returns>The parsed configuration and error message (if any)</returns>
    private static (GeneratorConfig? Config, string? ErrorMessage) ParseConfigurationAttribute(AttributeData attributeData)
    {
        var config = new GeneratorConfig();

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "DefaultRoutePrefix":
                    var routePrefix = namedArgument.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(routePrefix))
                    {
                        // Validate route prefix format
                        if (routePrefix!.StartsWith("/") || routePrefix.EndsWith("/"))
                        {
                            return (null, $"Invalid DefaultRoutePrefix '{routePrefix}'. Route prefix should not start or end with '/'.");
                        }
                    }
                    config.DefaultRoutePrefix = routePrefix;
                    break;
                case "DomainName":
                    config.DomainName = namedArgument.Value.Value?.ToString();
                    break;
                case "RequireExplicitRoutes":
                    if (namedArgument.Value.Value is bool requireExplicit)
                        config.RequireExplicitRoutes = requireExplicit;
                    break;
                case "SkipGeneration":
                    if (namedArgument.Value.Value is bool skipGeneration)
                        config.SkipGeneration = skipGeneration;
                    break;
            }
        }

        // Validate configuration consistency
        if (!config.RequireExplicitRoutes && string.IsNullOrEmpty(config.DefaultRoutePrefix))
        {
            return (null, "When RequireExplicitRoutes is false, DefaultRoutePrefix must be specified.");
        }

        return (config, null);
    }
}