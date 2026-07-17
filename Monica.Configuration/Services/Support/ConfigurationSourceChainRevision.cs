using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Creates an opaque revision for the provider topology that currently controls one configuration path.
/// </summary>
internal static class ConfigurationSourceChainRevision
{
    public static string Compute(ConfigurationSourceChain chain)
    {
        var content = JsonSerializer.Serialize(new
        {
            chain.ConfigurationPath,
            Sources = chain.Values.Select(static value => new
            {
                value.Source.SourceKey,
                value.Source.PriorityIndex,
                value.Source.Kind,
                value.Source.IsWritable,
                value.IsEffective
            })
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
