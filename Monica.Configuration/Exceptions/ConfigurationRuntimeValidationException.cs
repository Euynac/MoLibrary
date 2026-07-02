using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when the current runtime configuration does not satisfy Monica-managed schema validation rules.
/// </summary>
public sealed class ConfigurationRuntimeValidationException(ConfigurationValidationReport report)
    : InvalidOperationException(ConfigurationRuntimeValidationMessageFormatter.FormatReport(report))
{
    /// <summary>
    /// Gets the structured validation report that caused startup to fail.
    /// </summary>
    public ConfigurationValidationReport Report { get; } = report;
}
