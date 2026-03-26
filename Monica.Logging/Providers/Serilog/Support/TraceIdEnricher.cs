using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Monica.Logging.Providers.Serilog.Support;

/// <summary>
/// Enriches log events with an Activity trace id when available.
/// </summary>
internal sealed class TraceIdEnricher : ILogEventEnricher
{
    private const string TraceIdPropertyName = "TraceId";

    private LogEventProperty? _lastValue;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var last = _lastValue;

        if (last is null || (string?)((ScalarValue)last.Value).Value != traceId)
        {
            _lastValue = last = new LogEventProperty(TraceIdPropertyName, new ScalarValue(traceId));
        }

        logEvent.AddPropertyIfAbsent(last);
    }
}
