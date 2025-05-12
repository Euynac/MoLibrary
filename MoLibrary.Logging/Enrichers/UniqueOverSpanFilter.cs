using System.Runtime.Caching;
using Serilog.Core;
using Serilog.Events;

namespace MoLibrary.Logging.Enrichers;

public class UniqueOverSpanFilter(Func<LogEvent, bool> isEnabled, TimeSpan span) : ILogEventFilter
{
    private static readonly MemoryCache Cache;
    private readonly Func<LogEvent, bool> _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));

    static UniqueOverSpanFilter()
    {
        Cache = new MemoryCache("UniqueLogEntries");
    }

    public bool IsEnabled(LogEvent @event)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        if (_isEnabled(@event))
        {
            var key = @event
                .MessageTemplate
                .Render(@event.Properties)
                .GetHashCode()
                .ToString();

            if (Cache.Contains(key))
            {
                return false;
            }

            Cache
                .Add
                (
                    key,
                    key, // We're not really caching anything
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration = new DateTimeOffset(DateTime.UtcNow.Add(span))
                    }
                );
        }

        return true;
    }
}