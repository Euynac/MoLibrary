using Serilog;
using Serilog.Configuration;

namespace MoLibrary.Logging.Enrichers;

/// <summary>
/// Extends <see cref="LoggerConfiguration"/> to add enrichers for <see cref="Environment.CurrentManagedThreadId"/>
/// capabilities.
/// </summary>
public static class ThreadLoggerConfigurationExtensions
{
    /// <summary>
    /// Enrich log events with a ThreadId property containing the <see cref="Environment.CurrentManagedThreadId"/>.
    /// </summary>
    /// <param name="enrichmentConfiguration">Logger enrichment configuration.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="enrichmentConfiguration"/> is null.</exception>
    public static LoggerConfiguration WithThreadId(
        this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With<ThreadIdEnricher>();
    }

    /// <summary>
    /// Enrich log events with a ThreadName property containing the <see cref="Thread.CurrentThread"/> <see cref="Thread.Name"/>.
    /// </summary>
    /// <param name="enrichmentConfiguration">Logger enrichment configuration.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="enrichmentConfiguration"/> is null.</exception>
    public static LoggerConfiguration WithThreadName(
        this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With<ThreadNameEnricher>();
    }
}