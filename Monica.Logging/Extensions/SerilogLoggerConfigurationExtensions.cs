using System.Diagnostics;
using Monica.Logging.Providers.Serilog.Support;
using Serilog;
using Serilog.Configuration;

namespace Monica.Logging.Extensions;

/// <summary>
/// Extends Serilog configuration with Monica-specific enrichers.
/// </summary>
public static class SerilogLoggerConfigurationExtensions
{
    /// <summary>
    /// Enriches log events with the current managed thread id.
    /// </summary>
    public static LoggerConfiguration WithThreadId(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        return enrichmentConfiguration.With<ThreadIdEnricher>();
    }

    /// <summary>
    /// Enriches log events with the current thread name when available.
    /// </summary>
    public static LoggerConfiguration WithThreadName(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        return enrichmentConfiguration.With<ThreadNameEnricher>();
    }

    /// <summary>
    /// Enriches log events with the current activity trace id when available.
    /// </summary>
    public static LoggerConfiguration WithTraceId(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        return enrichmentConfiguration.With<TraceIdEnricher>();
    }
}
