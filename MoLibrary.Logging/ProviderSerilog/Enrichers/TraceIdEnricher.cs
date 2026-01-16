using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace MoLibrary.Logging.ProviderSerilog.Enrichers
{
    public class TraceIdEnricher : ILogEventEnricher
    {
        const string TraceIdPropertyName = "TraceId";

        private LogEventProperty? _lastValue;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var traceId = Activity.Current?.TraceId.ToString();
            var last = _lastValue;

            if (last is null || (string?) ((ScalarValue) last.Value).Value! != traceId)
            {
                _lastValue = last = new LogEventProperty(TraceIdPropertyName, new ScalarValue(traceId));
            }

            logEvent.AddPropertyIfAbsent(last);
        }
    }
}
