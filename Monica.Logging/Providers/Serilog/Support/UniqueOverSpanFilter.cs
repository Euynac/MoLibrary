using System.Runtime.Caching;
using Serilog.Core;
using Serilog.Events;

namespace Monica.Logging.Providers.Serilog.Support;

/// <summary>
/// Filters out duplicate rendered messages within a rolling time span.
/// </summary>
internal sealed class UniqueOverSpanFilter : ILogEventFilter, IDisposable
{
    private readonly MemoryCache _cache = new($"Monica.UniqueLogEntries.{Guid.NewGuid():N}");
    private readonly Func<LogEvent, bool> _isEnabled;
    private readonly TimeSpan _span;

    public UniqueOverSpanFilter(Func<LogEvent, bool> isEnabled, TimeSpan span)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(span, TimeSpan.Zero);
        _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
        _span = span;
    }

    public bool IsEnabled(LogEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (!_isEnabled(@event))
        {
            return true;
        }

        var key = @event.MessageTemplate.Render(@event.Properties);
        return _cache.Add(
            key,
            key,
            new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(_span)
            });
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
