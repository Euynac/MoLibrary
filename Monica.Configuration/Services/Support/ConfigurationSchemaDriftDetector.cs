using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Detects schema drift between two definitions of the same key.
/// </summary>
internal sealed class ConfigurationSchemaDriftDetector
{
    /// <summary>
    /// Checks whether two definitions have different schema hashes.
    /// </summary>
    /// <param name="left">The first definition.</param>
    /// <param name="right">The second definition.</param>
    /// <returns>True when the schema hashes differ.</returns>
    public bool HasDrift(ConfigurationDefinition left, ConfigurationDefinition right)
    {
        return !string.Equals(left.SchemaHash, right.SchemaHash, StringComparison.Ordinal);
    }
}
