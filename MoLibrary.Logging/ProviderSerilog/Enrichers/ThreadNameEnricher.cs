using Serilog.Core;
using Serilog.Events;

namespace MoLibrary.Logging.ProviderSerilog.Enrichers;

/// <summary>
/// Enriches log events with a ThreadName property containing the <see cref="Thread.CurrentThread"/> <see cref="Thread.Name"/>.
/// </summary>
sealed class ThreadNameEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name added to enriched log events.
    /// </summary>
    const string ThreadNamePropertyName = "ThreadName";

    /// <summary>
    /// The cached last created "ThreadName" property with some thread name. It is likely to be reused frequently so avoiding heap allocations.
    /// </summary>
    private LogEventProperty? _lastValue;

    /// <summary>
    /// Enrich the log event.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var threadName = Thread.CurrentThread.Name;
        if (threadName is not null)
        {
            var last = _lastValue;
            if (last is null || (string) ((ScalarValue) last.Value).Value! != threadName)
                // no need to synchronize threads on write - just some of them will win
                _lastValue = last = new LogEventProperty(ThreadNamePropertyName, new ScalarValue(threadName));

            logEvent.AddPropertyIfAbsent(last);
        }
    }
}