using Monica.Logging.Providers.Serilog.Support;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Monica.Logging.Extensions;

/// <summary>
/// Extends Serilog filter configuration with Monica-specific helpers.
/// </summary>
public static class SerilogLoggerFilterExtensions
{
    /// <summary>
    /// Filters out duplicate log messages that repeat within the specified time span.
    /// </summary>
    public static LoggerConfiguration UniqueOverSpan(
        this LoggerFilterConfiguration configuration,
        Func<LogEvent, bool> inclusionPredicate,
        TimeSpan span)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(inclusionPredicate);
        return configuration.With(new UniqueOverSpanFilter(inclusionPredicate, span));
    }
}
