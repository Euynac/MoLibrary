using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when fail-fast runtime validation rejects effective values that do not satisfy Monica-managed schema rules.
/// </summary>
public sealed class ConfigurationRuntimeValidationException(ConfigurationValidationReport report)
    : InvalidOperationException(ConfigurationRuntimeValidationMessageFormatter.FormatFailFastReport(report))
{
    /// <summary>
    /// Gets the structured validation report that triggered fail-fast enforcement.
    /// </summary>
    public ConfigurationValidationReport Report { get; } = report;
}
