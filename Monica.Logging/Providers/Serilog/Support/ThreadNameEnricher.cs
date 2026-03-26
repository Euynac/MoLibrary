using Serilog.Core;
using Serilog.Events;

namespace Monica.Logging.Providers.Serilog.Support;

/// <summary>
/// Enriches log events with a ThreadName property containing the current thread name.
/// </summary>
internal sealed class ThreadNameEnricher : ILogEventEnricher
{
    private const string ThreadNamePropertyName = "ThreadName";

    private LogEventProperty? _lastValue;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var threadName = Thread.CurrentThread.Name;
        if (threadName is null)
        {
            return;
        }

        var last = _lastValue;
        if (last is null || (string)((ScalarValue)last.Value).Value! != threadName)
        {
            _lastValue = last = new LogEventProperty(ThreadNamePropertyName, new ScalarValue(threadName));
        }

        logEvent.AddPropertyIfAbsent(last);
    }
}
