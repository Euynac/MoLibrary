using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Decides whether a configuration definition participates in unified version snapshots.
/// </summary>
/// <remarks>
/// Implementations should be deterministic for a given definition. Runtime operators expect one
/// unified version to contain the same configured definition set throughout one capture operation.
/// </remarks>
public interface IConfigurationUnifiedVersionFilter
{
    /// <summary>
    /// Determines whether the specified definition should be captured in unified parameter versions.
    /// </summary>
    /// <param name="definition">The configuration definition being evaluated.</param>
    /// <returns><c>true</c> when the definition should be included; otherwise <c>false</c>.</returns>
    bool ShouldInclude(ConfigurationDefinition definition);
}
