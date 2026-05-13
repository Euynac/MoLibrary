using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Builds configuration definitions from CLR options types.
/// </summary>
public interface IConfigurationDefinitionScanner
{
    /// <summary>
    /// Scans one options type.
    /// </summary>
    /// <param name="optionsType">The options type.</param>
    /// <returns>The discovered definition.</returns>
    ConfigurationDefinition Scan(Type optionsType);
}
