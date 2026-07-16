using System.Security.Cryptography;
using System.Text;

namespace Monica.Configuration.Models;

/// <summary>
/// Computes the provider-independent identity used to compare configuration definition keys.
/// </summary>
/// <remarks>
/// Definition keys retain their original casing for display and audit records. Persistence stores the normalized
/// identity separately as a fixed uppercase SHA-256 value. The restricted hexadecimal representation makes indexed
/// equality independent of database collation, identifier length, and the original key's Unicode content.
/// </remarks>
public static class ConfigurationDefinitionIdentity
{
    /// <summary>
    /// Gets the exact length of a persisted definition identity.
    /// </summary>
    public const int Length = 64;

    /// <summary>
    /// Computes the persisted identity used for case-insensitive definition-key comparison.
    /// </summary>
    /// <param name="definitionKey">The non-empty definition key.</param>
    /// <returns>The fixed-length SHA-256 identity of the invariant-uppercase key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="definitionKey"/> is empty or whitespace.</exception>
    public static string Compute(string definitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        var canonicalKey = definitionKey.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalKey)));
    }
}
