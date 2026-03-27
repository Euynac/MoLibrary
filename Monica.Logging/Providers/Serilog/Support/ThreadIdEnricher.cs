using Serilog.Core;
using Serilog.Events;

namespace Monica.Logging.Providers.Serilog.Support;

/// <summary>
/// Enriches log events with a ThreadId property containing the current managed thread id.
/// </summary>
internal sealed class ThreadIdEnricher : ILogEventEnricher
{
    private const string ThreadIdPropertyName = "ThreadId";

    private LogEventProperty? _lastValue;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var threadId = Environment.CurrentManagedThreadId;
        var last = _lastValue;

        if (last is null || (int)((ScalarValue)last.Value).Value! != threadId)
        {
            _lastValue = last = new LogEventProperty(ThreadIdPropertyName, new ScalarValue(threadId));
        }

        logEvent.AddPropertyIfAbsent(last);
    }
}
