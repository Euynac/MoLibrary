using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Utils;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Enforces the no-overlap invariant between container snapshots and descendant leaf overrides.
/// </summary>
internal sealed class ConfigurationOverrideNormalizer : IConfigurationOverrideNormalizer
{
    /// <inheritdoc />
    public IReadOnlyList<ConfigurationValueOverride> Normalize(IReadOnlyList<ConfigurationValueOverride> overrides)
    {
        return overrides
            .GroupBy(x => x.SourceKey, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => NormalizeSource(group.ToArray()))
            .ToArray();
    }

    private static IReadOnlyList<ConfigurationValueOverride> NormalizeSource(IReadOnlyList<ConfigurationValueOverride> overrides)
    {
        var ordered = overrides
            .OrderBy(x => x.LogicalPath.Depth)
            .ThenBy(x => x.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
            .ToList();

        var result = new List<ConfigurationValueOverride>();
        foreach (var current in ordered)
        {
            var coveredByContainer = result.Any(existing =>
                existing.Granularity == ConfigurationOverrideGranularity.Container
                && ConfigurationPathTokenizer.StartsWith(current.LogicalPath, existing.LogicalPath)
                && current.LogicalPath != existing.LogicalPath);

            if (!coveredByContainer)
            {
                result.RemoveAll(existing =>
                    current.Granularity == ConfigurationOverrideGranularity.Container
                    && ConfigurationPathTokenizer.StartsWith(existing.LogicalPath, current.LogicalPath)
                    && existing.LogicalPath != current.LogicalPath);
                result.Add(current);
            }
        }

        return result;
    }
}
