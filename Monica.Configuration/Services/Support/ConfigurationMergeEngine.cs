using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Utils;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Applies source priority and subtree removal semantics.
/// </summary>
internal sealed class ConfigurationMergeEngine : IConfigurationMergeEngine
{
    /// <inheritdoc />
    public IReadOnlyList<MergedNodeValue> Merge(IReadOnlyList<NormalizedOverrideSet> sourceSets)
    {
        var ordered = sourceSets
            .OrderByDescending(x => x.Source.Priority)
            .ToArray();
        var removedSubtrees = new List<(string DefinitionKey, LogicalPath Path)>();
        var effective = new Dictionary<(string DefinitionKey, string Path), ConfigurationValueOverride>();

        foreach (var sourceSet in ordered)
        {
            foreach (var value in sourceSet.Overrides)
            {
                if (removedSubtrees.Any(x =>
                        string.Equals(x.DefinitionKey, value.DefinitionKey, StringComparison.OrdinalIgnoreCase)
                        && ConfigurationPathTokenizer.StartsWith(value.LogicalPath, x.Path)))
                {
                    continue;
                }

                if (value.State == ConfigurationValueState.RemovedSubtree)
                {
                    removedSubtrees.Add((value.DefinitionKey, value.LogicalPath));
                    continue;
                }

                if (value.State != ConfigurationValueState.Active)
                {
                    continue;
                }

                var key = (value.DefinitionKey, value.LogicalPath.ToCanonicalString());
                effective.TryAdd(key, value);
            }
        }

        return effective.Values
            .Select(value => new MergedNodeValue
            {
                DefinitionKey = value.DefinitionKey,
                LogicalPath = value.LogicalPath,
                Override = value
            })
            .ToArray();
    }
}
