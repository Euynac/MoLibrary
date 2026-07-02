using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Builds source-aware runtime validation reports for Monica-managed configuration definitions.
/// </summary>
public interface IConfigurationRuntimeValidationService
{
    /// <summary>
    /// Builds a validation report for every local configuration definition.
    /// </summary>
    /// <returns>The validation report.</returns>
    ConfigurationValidationReport GetReport();

    /// <summary>
    /// Builds a validation report for one local configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The validation report.</returns>
    ConfigurationValidationReport GetReport(string definitionKey);
}
