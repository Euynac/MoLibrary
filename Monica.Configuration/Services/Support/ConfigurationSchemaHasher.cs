using System.Security.Cryptography;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Computes stable schema hashes for configuration definitions.
/// </summary>
internal sealed class ConfigurationSchemaHasher
{
    /// <summary>
    /// Computes a SHA-256 hash from definition identity and structure.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="sectionPath">The section path.</param>
    /// <param name="root">The root schema node.</param>
    /// <returns>The hash string.</returns>
    public string ComputeHash(string definitionKey, string sectionPath, ConfigurationNodeDefinition root)
    {
        var json = ConfigurationDefinitionSchemaCodec.SerializeHashInput(definitionKey, sectionPath, root);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
