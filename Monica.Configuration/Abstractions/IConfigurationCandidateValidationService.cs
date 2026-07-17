using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Validates complete candidate configuration values against the active mutation-time schema.
/// </summary>
public interface IConfigurationCandidateValidationService
{
    /// <summary>
    /// Validates a complete JSON value for one definition scope.
    /// </summary>
    /// <param name="definition">The active configuration definition that owns the candidate value.</param>
    /// <param name="scopePath">The logical path whose complete value is represented by <paramref name="json"/>.</param>
    /// <param name="json">The normalized candidate JSON value.</param>
    /// <returns>A structured report containing every mutation-time validation issue.</returns>
    ConfigurationCandidateValidationReport Validate(
        ConfigurationDefinition definition,
        LogicalPath scopePath,
        string json);
}
