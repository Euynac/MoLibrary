using Monica.ProjectUnits.Abstractions;
using Monica.Tool.Extensions;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Monica.ProjectUnits.Providers.Serilog;

/// <summary>
/// Provides Serilog configuration extensions for host-scoped ProjectUnits metadata.
/// </summary>
public static class DynamicSourceContextLoggerConfigurationExtensions
{
    /// <summary>
    /// Replaces source-context values with project-unit display names when metadata is available.
    /// </summary>
    /// <param name="enrichmentConfiguration">The Serilog enrichment configuration.</param>
    /// <param name="catalog">The project-unit catalog resolved from the Monica host being configured.</param>
    /// <returns>The owning logger configuration.</returns>
    public static LoggerConfiguration WithDynamicSourceContext(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        IProjectUnitCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        ArgumentNullException.ThrowIfNull(catalog);
        return enrichmentConfiguration.With(new DynamicSourceEnricher(catalog));
    }
}

/// <summary>
/// Enriches Serilog events with host-scoped project-unit display metadata.
/// </summary>
public sealed class DynamicSourceEnricher(IProjectUnitCatalog catalog) : ILogEventEnricher
{
    private const string PROPERTIES_TEMPLATE_NAME = "Properties";

    /// <summary>
    /// Gets the Serilog source-context property name.
    /// </summary>
    public const string SOURCE_CONTEXT_TEMPLATE_NAME = "SourceContext";

    /// <summary>
    /// Gets the property name used to mark a project-unit source.
    /// </summary>
    public const string TYPE_TEMPLATE_NAME = "UnitType";

    /// <summary>
    /// Enrich the log event.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var properties = (Dictionary<string, LogEventPropertyValue>?)logEvent.GetPropertyValue(PROPERTIES_TEMPLATE_NAME);
        if (properties?.TryGetValue(SOURCE_CONTEXT_TEMPLATE_NAME, out var propertyValue) is true)
        {
            var name = catalog.FindByFullName(propertyValue.ToString().Trim('\"'))?.MetadataTitle;

            if (name != null)
            {
                properties[SOURCE_CONTEXT_TEMPLATE_NAME] = new ScalarValue(name);
                properties.Add(TYPE_TEMPLATE_NAME, new ScalarValue("[T:B]"));
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty(PROPERTIES_TEMPLATE_NAME, new ScalarValue(properties)));
            }
        }
    }
}
