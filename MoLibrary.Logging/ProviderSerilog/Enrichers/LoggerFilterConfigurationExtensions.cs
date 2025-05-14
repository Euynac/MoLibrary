using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace MoLibrary.Logging.ProviderSerilog.Enrichers;

public static class LoggerFilterConfigurationExtensions
{
    public static LoggerConfiguration UniqueOverSpan(this LoggerFilterConfiguration configuration, Func<LogEvent, bool> inclusionPredicate, TimeSpan span)
    {
        return
            configuration
                .With(new UniqueOverSpanFilter(inclusionPredicate, span));
    }

    //使用Serilog Expressions实现
    //public static LoggerConfiguration UniqueOverSpan(this LoggerFilterConfiguration loggerFilterConfiguration, string expression, TimeSpan span)
    //{
    //    if (loggerFilterConfiguration == null)
    //    {
    //        throw new ArgumentNullException(nameof(loggerFilterConfiguration));
    //    }

    //    if (expression == null)
    //    {
    //        throw new ArgumentNullException(nameof(expression));
    //    }

    //    var compiled = FilterLanguage.CreateFilter(expression);

    //    return
    //        loggerFilterConfiguration
    //            .UniqueOverSpan
    //            (
    //                e => true.Equals(compiled(e)),
    //                span
    //            );
    //}
}