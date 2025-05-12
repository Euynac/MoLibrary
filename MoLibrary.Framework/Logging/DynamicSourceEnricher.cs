using MoLibrary.Framework.Core;
using MoLibrary.Framework.Core.Attributes;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using ShardingCore.Extensions;

namespace MoLibrary.Framework.Logging;
public static class DynamicSourceContextLoggerConfigurationExtensions
{
    public static LoggerConfiguration WithDynamicSourceContext(
        this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With<DynamicSourceEnricher>();
    }
}
public sealed class DynamicSourceEnricher : ILogEventEnricher
{
    private const string Properties = "Properties";
    public const string SOURCE_CONTEXT_TEMPLATE_NAME = "SourceContext";
    public const string TYPE_TEMPLATE_NAME = "UnitType";

    private LogEventProperty? _lastValue;

    /// <summary>
    /// Enrich the log event.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
            
        var last = _lastValue;
        var properties = (Dictionary<string, LogEventPropertyValue>) logEvent.GetPropertyValue(Properties);
        if (properties.TryGetValue(SOURCE_CONTEXT_TEMPLATE_NAME, out var propertyValue))
        {
            var unitInfoAttribute =
                ProjectUnitStores.GetUnitAttributeByFullName<UnitInfoAttribute>(propertyValue.ToString().Trim('\"'));

            var name = unitInfoAttribute?.Group ?? unitInfoAttribute?.Name;
                
            if (name != null)
            {
                properties[SOURCE_CONTEXT_TEMPLATE_NAME] = new ScalarValue(name);
                properties.Add(TYPE_TEMPLATE_NAME, new ScalarValue("[T:B]"));
                _lastValue = last = new LogEventProperty(Properties, new ScalarValue(properties));
                logEvent.AddOrUpdateProperty(_lastValue); 
            }
        }
    }
}