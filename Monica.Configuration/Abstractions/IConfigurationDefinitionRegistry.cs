using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Stores configuration definitions known to the current process.
/// </summary>
public interface IConfigurationDefinitionRegistry
{
    /// <summary>
    /// Registers or replaces a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    void Register(ConfigurationDefinition definition);

    /// <summary>
    /// Gets all registered definitions.
    /// </summary>
    /// <returns>The definitions.</returns>
    IReadOnlyList<ConfigurationDefinition> GetAll();

    /// <summary>
    /// Gets a definition by key.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The matching definition.</returns>
    ConfigurationDefinition GetRequired(string definitionKey);

    /// <summary>
    /// Attempts to get a definition by key.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="definition">The matching definition when found.</param>
    /// <returns>True when found.</returns>
    bool TryGet(string definitionKey, out ConfigurationDefinition? definition);
}
