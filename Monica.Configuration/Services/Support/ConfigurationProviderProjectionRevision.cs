using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Creates an opaque revision for the schema-visible scalar values contributed by one configuration provider.
/// </summary>
internal static class ConfigurationProviderProjectionRevision
{
    private static readonly byte[] PROCESS_SALT = RandomNumberGenerator.GetBytes(32);

    public static string Compute(IConfigurationProvider provider, ConfigurationDefinition definition)
    {
        var entries = EnumerateProviderValues(provider, definition.SectionPath)
            .Where(static entry => entry.Value is not null)
            .Select(entry => new
            {
                Entry = entry,
                Node = ResolveScalarNode(definition, entry.ConfigurationPath)
            })
            .Where(static entry => entry.Node is not null)
            .OrderBy(static entry => entry.Entry.ConfigurationPath, StringComparer.OrdinalIgnoreCase)
            .Select(static entry => new ProjectionEntry(
                entry.Entry.ConfigurationPath.ToUpperInvariant(),
                NormalizeScalar(entry.Node!, entry.Entry.Value!)));
        var content = new StringBuilder();
        foreach (var entry in entries)
        {
            content.Append(entry.Path.Length)
                .Append(':')
                .Append(entry.Path)
                .Append(entry.Value.Length)
                .Append(':')
                .Append(entry.Value);
        }

        var contentBytes = Encoding.UTF8.GetBytes(content.ToString());
        var saltedContent = new byte[PROCESS_SALT.Length + contentBytes.Length];
        PROCESS_SALT.CopyTo(saltedContent, 0);
        contentBytes.CopyTo(saltedContent, PROCESS_SALT.Length);
        var hash = SHA256.HashData(saltedContent);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static IEnumerable<(string ConfigurationPath, string? Value)> EnumerateProviderValues(
        IConfigurationProvider provider,
        string? parentPath)
    {
        if (!string.IsNullOrWhiteSpace(parentPath) && provider.TryGet(parentPath, out var parentValue))
        {
            yield return (parentPath, parentValue);
        }

        var childKeys = provider.GetChildKeys([], parentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase);
        foreach (var childKey in childKeys)
        {
            var configurationPath = string.IsNullOrWhiteSpace(parentPath)
                ? childKey
                : $"{parentPath}:{childKey}";
            foreach (var descendant in EnumerateProviderValues(provider, configurationPath))
            {
                yield return descendant;
            }
        }
    }

    private static ConfigurationNodeDefinition? ResolveScalarNode(
        ConfigurationDefinition definition,
        string configurationPath)
    {
        var sectionSegments = SplitPath(definition.SectionPath);
        var pathSegments = SplitPath(configurationPath);
        if (pathSegments.Length < sectionSegments.Length || !HasPrefix(pathSegments, sectionSegments))
        {
            return null;
        }

        var current = definition.Root;
        for (var index = sectionSegments.Length; index < pathSegments.Length; index++)
        {
            current = current.NodeKind switch
            {
                ConfigurationNodeKind.Object => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, pathSegments[index], StringComparison.OrdinalIgnoreCase)),
                ConfigurationNodeKind.Dictionary => current.DictionaryTemplate?.ValueTemplate,
                ConfigurationNodeKind.List when int.TryParse(pathSegments[index], out _) =>
                    current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current.NodeKind == ConfigurationNodeKind.Scalar ? current : null;
    }

    private static string NormalizeScalar(ConfigurationNodeDefinition node, string value)
    {
        return node.ValueKind switch
        {
            ConfigurationValueKind.String => ConfigurationRegexTextCodec.NormalizeDisplayValue(node, value) ?? string.Empty,
            ConfigurationValueKind.Boolean when bool.TryParse(value, out var parsed) =>
                parsed ? bool.TrueString : bool.FalseString,
            ConfigurationValueKind.Integer when long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed.ToString(CultureInfo.InvariantCulture),
            ConfigurationValueKind.Decimal when decimal.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed.ToString(CultureInfo.InvariantCulture),
            ConfigurationValueKind.Floating when double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed.ToString("R", CultureInfo.InvariantCulture),
            ConfigurationValueKind.Enum when node.TryNormalizeEnumDisplayValue(value, out var normalized) =>
                normalized.ToUpperInvariant(),
            ConfigurationValueKind.DateTime when DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed) => parsed.ToString("O", CultureInfo.InvariantCulture),
            ConfigurationValueKind.TimeSpan when TimeSpan.TryParse(
                value,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed.ToString("c", CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private static string[] SplitPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasPrefix(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        for (var index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(path[index], prefix[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record ProjectionEntry(string Path, string Value);
}
