using System.Security.Cryptography;
using System.Text;

namespace Monica.Configuration.EfCore.Stores.Support;

/// <summary>
/// Produces a fixed, case-insensitive publisher identity whose equality is independent of database collation.
/// </summary>
internal static class PublishedDefinitionPublisherIdentity
{
    internal static string Compute(string publisherKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisherKey);
        var canonicalKey = publisherKey.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalKey)));
    }
}
