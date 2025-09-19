using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Diagnostics;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Helpers;

internal static class ConfigurationHelper
{
    /// <summary>
    /// Extracts the generator configuration from assembly-level attributes with error reporting.
    /// </summary>
    /// <param name="compilation">The compilation context</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>The extracted configuration or null if extraction failed</returns>
    public static GeneratorConfig? ExtractConfiguration(Compilation compilation, SourceProductionContext context)
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
                return GeneratorConfig.Default;
            }

            return ParseConfigurationAttribute(configAttribute, context);
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConfigurationExtractionFailed,
                Location.None,
                ex.Message);
            context.ReportDiagnostic(diagnostic);
            return null;
        }
    }

    /// <summary>
    /// Extracts the generator configuration from assembly-level attributes (compatibility overload).
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
    /// Parses the configuration from an attribute data instance with validation and error reporting.
    /// </summary>
    /// <param name="attributeData">The attribute data to parse</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>The parsed configuration or null if parsing failed</returns>
    private static GeneratorConfig? ParseConfigurationAttribute(AttributeData attributeData, SourceProductionContext context)
    {
        var config = new GeneratorConfig();
        var errors = new List<string>();

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "DefaultRoutePrefix":
                    var routePrefix = namedArgument.Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(routePrefix))
                    {
                        // Validate route prefix format
                        if (routePrefix.StartsWith("/") || routePrefix.EndsWith("/"))
                        {
                            var diagnostic = Diagnostic.Create(
                                DiagnosticDescriptors.InvalidDefaultRoutePrefix,
                                Location.None,
                                routePrefix);
                            context.ReportDiagnostic(diagnostic);
                            return null;
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
            }
        }

        // Validate configuration consistency
        if (!config.RequireExplicitRoutes && string.IsNullOrEmpty(config.DefaultRoutePrefix))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingConfigurationAttribute,
                Location.None);
            context.ReportDiagnostic(diagnostic);
            return null;
        }

        return config;
    }

    /// <summary>
    /// Parses the configuration from an attribute data instance (compatibility overload).
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