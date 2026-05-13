using System.Security.Cryptography;
using System.Text.Json;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

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
        var input = new DefinitionSchemaHashInput
        {
            DefinitionKey = definitionKey,
            SectionPath = sectionPath,
            Root = root
        };
        var json = JsonSerializer.Serialize(input);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
