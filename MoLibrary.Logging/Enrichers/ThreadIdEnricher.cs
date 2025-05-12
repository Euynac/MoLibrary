using Serilog.Core;
using Serilog.Events;

namespace MoLibrary.Logging.Enrichers;

/// <summary>
/// Enriches log events with a ThreadId property containing the <see cref="Environment.CurrentManagedThreadId"/>.
/// </summary>
sealed class ThreadIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name added to enriched log events.
    /// </summary>
    const string ThreadIdPropertyName = "ThreadId";

    /// <summary>
    /// The cached last created "ThreadId" property with some thread id. It is likely to be reused frequently so avoiding heap allocations.
    /// </summary>
    private LogEventProperty? _lastValue;

    /// <summary>
    /// Enrich the log event.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var threadId = Environment.CurrentManagedThreadId;
        var last = _lastValue;
        if (last is null || (int) ((ScalarValue) last.Value).Value! != threadId)
            // no need to synchronize threads on write - just some of them will win
            _lastValue = last = new LogEventProperty(ThreadIdPropertyName, new ScalarValue(threadId));

        logEvent.AddPropertyIfAbsent(last);
    }
}