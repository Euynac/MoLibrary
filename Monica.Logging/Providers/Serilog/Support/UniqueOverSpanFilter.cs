using System.Runtime.Caching;
using Serilog.Core;
using Serilog.Events;

namespace Monica.Logging.Providers.Serilog.Support;

/// <summary>
/// Filters out duplicate rendered messages within a rolling time span.
/// </summary>
internal sealed class UniqueOverSpanFilter(Func<LogEvent, bool> isEnabled, TimeSpan span) : ILogEventFilter
{
    private static readonly MemoryCache Cache = new("UniqueLogEntries");

    private readonly Func<LogEvent, bool> _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));

    public bool IsEnabled(LogEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (!_isEnabled(@event))
        {
            return true;
        }

        var key = @event.MessageTemplate.Render(@event.Properties).GetHashCode().ToString();
        if (Cache.Contains(key))
        {
            return false;
        }

        Cache.Add(
            key,
            key,
            new CacheItemPolicy
            {
                AbsoluteExpiration = new DateTimeOffset(DateTime.UtcNow.Add(span))
            });

        return true;
    }
}
