using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationHistoryDiffSnapshotBuilder
{
    private const string ROOT_KEY = "$root";

    public static string BuildOriginSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        bool isRollbackPreview,
        IReadOnlyDictionary<string, ConfigurationStoredValue?> currentValuesByHistoryId,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
        Func<ConfigurationValueHistory, string> targetDisplay,
        string redactedLabel)
    {
        return BuildSnapshot(
            rows,
            group => isRollbackPreview
                ? CurrentValue(group.Latest, currentValuesByHistoryId)
                : group.Earliest.OldValue,
            isSensitive,
            definitionDisplay,
            targetDisplay,
            redactedLabel);
    }

    private static ConfigurationStoredValue? CurrentValue(
        ConfigurationValueHistory latest,
        IReadOnlyDictionary<string, ConfigurationStoredValue?> currentValuesByHistoryId)
    {
        return currentValuesByHistoryId.TryGetValue(latest.HistoryId, out var currentValue)
            ? currentValue
            : null;
    }

    public static string BuildTargetSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        bool isRollbackPreview,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
        Func<ConfigurationValueHistory, string> targetDisplay,
        string redactedLabel)
    {
        return BuildSnapshot(
            rows,
            group => isRollbackPreview ? group.Earliest.OldValue : group.Latest.NewValue,
            isSensitive,
            definitionDisplay,
            targetDisplay,
            redactedLabel);
    }

    private static string BuildSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        Func<HistoryPathGroup, ConfigurationStoredValue?> selectValue,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
        Func<ConfigurationValueHistory, string> targetDisplay,
        string redactedLabel)
    {
        var groups = CollapseByPath(rows).ToArray();
        if (groups.Length == 0)
        {
            return "null";
        }

        if (groups.Length == 1 && groups[0].Earliest.LogicalPath.Depth == 0)
        {
            return FormatJson(CreateNode(selectValue(groups[0]), isSensitive(groups[0].Earliest), redactedLabel));
        }

        var includeDefinitionRoot = groups
            .Select(group => group.Earliest.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Any();
        var includeTargetRoot = groups
            .Select(static group => group.TargetIdentity)
            .Distinct(HistoryTargetKeyComparer.Instance)
            .Skip(1)
            .Any();
        var root = new JsonObject();

        foreach (var group in groups)
        {
            var row = group.Earliest;
            var node = CreateNode(selectValue(group), isSensitive(row), redactedLabel);
            var destinationRoot = root;
            if (includeTargetRoot)
            {
                var targetKey = targetDisplay(row);
                if (root[targetKey] is not JsonObject targetRoot)
                {
                    targetRoot = new JsonObject();
                    root[targetKey] = targetRoot;
                }

                destinationRoot = targetRoot;
            }

            if (includeDefinitionRoot)
            {
                var definitionKey = BuildDefinitionNodeKey(row.DefinitionKey, definitionDisplay);
                var definitionRoot = destinationRoot[definitionKey] as JsonObject;
                if (definitionRoot is null)
                {
                    definitionRoot = new JsonObject();
                    destinationRoot[definitionKey] = definitionRoot;
                }

                SetPath(definitionRoot, row.LogicalPath, node);
                continue;
            }

            SetPath(destinationRoot, row.LogicalPath, node);
        }

        return root.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions);
    }

    private static IReadOnlyList<HistoryPathGroup> CollapseByPath(IReadOnlyList<ConfigurationValueHistory> rows)
    {
        return rows
            .GroupBy(
                row => new HistoryPathKey(
                    BuildTargetKey(row),
                    row.DefinitionKey,
                    row.LogicalPath.ToCanonicalString()),
                HistoryPathKeyComparer.Instance)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(row => row.ModifiedTime)
                    .ThenBy(row => row.Version)
                    .ThenBy(row => row.HistoryId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new HistoryPathGroup(group.Key.Target, ordered[0], ordered[^1]);
            })
            .OrderBy(static group => group.TargetIdentity.TargetKind)
            .ThenBy(static group => group.TargetIdentity.PhysicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.TargetIdentity.ProviderType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.TargetIdentity.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Earliest.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Earliest.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HistoryTargetKey BuildTargetKey(ConfigurationValueHistory row)
    {
        if (row.TargetKind == ConfigurationMutationTargetKind.MonicaEffectiveStore)
        {
            return new HistoryTargetKey(row.TargetKind, null, null, null);
        }

        return string.IsNullOrWhiteSpace(row.SourcePhysicalPath)
            ? new HistoryTargetKey(
                row.TargetKind,
                PhysicalPath: null,
                row.SourceProviderType,
                row.SourceDisplayName)
            : new HistoryTargetKey(
                row.TargetKind,
                row.SourcePhysicalPath,
                ProviderType: null,
                DisplayName: null);
    }

    private static void SetPath(JsonObject root, LogicalPath path, JsonNode? value)
    {
        if (path.Depth == 0)
        {
            root[ROOT_KEY] = value;
            return;
        }

        var current = root;
        for (var i = 0; i < path.Segments.Count - 1; i++)
        {
            var key = SegmentKey(path.Segments[i]);
            if (current[key] is not JsonObject next)
            {
                next = new JsonObject();
                current[key] = next;
            }

            current = next;
        }

        current[SegmentKey(path.Segments[^1])] = value;
    }

    private static JsonNode? CreateNode(ConfigurationStoredValue? value, bool isSensitive, string redactedLabel)
    {
        if (isSensitive)
        {
            return JsonValue.Create(redactedLabel);
        }

        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value.Json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value.Json);
        }
    }

    private static string FormatJson(JsonNode? node)
    {
        return node?.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions) ?? "null";
    }

    private static string BuildDefinitionNodeKey(string definitionKey, Func<string, string> definitionDisplay)
    {
        var display = definitionDisplay(definitionKey);
        return string.Equals(display, definitionKey, StringComparison.OrdinalIgnoreCase)
            ? definitionKey
            : $"{display} [{definitionKey}]";
    }

    private static string SegmentKey(ConfigurationPathSegment segment)
    {
        return segment switch
        {
            PropertySegment property => property.Name,
            DictionaryKeySegment dictionaryKey => dictionaryKey.Key,
            ListItemKeySegment itemKey => itemKey.ItemKey,
            ListIndexSegment index => index.Index.ToString(CultureInfo.InvariantCulture),
            _ => segment.Value
        };
    }

    private sealed record HistoryTargetKey(
        ConfigurationMutationTargetKind TargetKind,
        string? PhysicalPath,
        string? ProviderType,
        string? DisplayName);

    private sealed record HistoryPathKey(
        HistoryTargetKey Target,
        string DefinitionKey,
        string LogicalPath);

    private sealed record HistoryPathGroup(
        HistoryTargetKey TargetIdentity,
        ConfigurationValueHistory Earliest,
        ConfigurationValueHistory Latest);

    private sealed class HistoryTargetKeyComparer : IEqualityComparer<HistoryTargetKey>
    {
        public static HistoryTargetKeyComparer Instance { get; } = new();

        public bool Equals(HistoryTargetKey? left, HistoryTargetKey? right)
        {
            return ReferenceEquals(left, right)
                   || left is not null
                   && right is not null
                   && left.TargetKind == right.TargetKind
                   && StringComparer.OrdinalIgnoreCase.Equals(left.PhysicalPath, right.PhysicalPath)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.ProviderType, right.ProviderType)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.DisplayName, right.DisplayName);
        }

        public int GetHashCode(HistoryTargetKey key)
        {
            var hash = new HashCode();
            hash.Add(key.TargetKind);
            hash.Add(key.PhysicalPath, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.ProviderType, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.DisplayName, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }

    private sealed class HistoryPathKeyComparer : IEqualityComparer<HistoryPathKey>
    {
        public static HistoryPathKeyComparer Instance { get; } = new();

        public bool Equals(HistoryPathKey? left, HistoryPathKey? right)
        {
            return ReferenceEquals(left, right)
                   || left is not null
                   && right is not null
                   && HistoryTargetKeyComparer.Instance.Equals(left.Target, right.Target)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.DefinitionKey, right.DefinitionKey)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.LogicalPath, right.LogicalPath);
        }

        public int GetHashCode(HistoryPathKey key)
        {
            var hash = new HashCode();
            hash.Add(HistoryTargetKeyComparer.Instance.GetHashCode(key.Target));
            hash.Add(key.DefinitionKey, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.LogicalPath, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
