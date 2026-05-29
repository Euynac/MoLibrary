using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationHistoryDiffSnapshotBuilder
{
    private const string ROOT_KEY = "$root";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    public static string BuildOriginSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        bool isRollbackPreview,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
        string redactedLabel)
    {
        return BuildSnapshot(
            rows,
            group => isRollbackPreview ? group.Latest.NewValue : group.Earliest.OldValue,
            isSensitive,
            definitionDisplay,
            redactedLabel);
    }

    public static string BuildTargetSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        bool isRollbackPreview,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
        string redactedLabel)
    {
        return BuildSnapshot(
            rows,
            group => isRollbackPreview ? group.Earliest.OldValue : group.Latest.NewValue,
            isSensitive,
            definitionDisplay,
            redactedLabel);
    }

    private static string BuildSnapshot(
        IReadOnlyList<ConfigurationValueHistory> rows,
        Func<HistoryPathGroup, ConfigurationStoredValue?> selectValue,
        Func<ConfigurationValueHistory, bool> isSensitive,
        Func<string, string> definitionDisplay,
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
        var root = new JsonObject();

        foreach (var group in groups)
        {
            var row = group.Earliest;
            var node = CreateNode(selectValue(group), isSensitive(row), redactedLabel);
            if (includeDefinitionRoot)
            {
                var definitionKey = BuildDefinitionNodeKey(row.DefinitionKey, definitionDisplay);
                var definitionRoot = root[definitionKey] as JsonObject;
                if (definitionRoot is null)
                {
                    definitionRoot = new JsonObject();
                    root[definitionKey] = definitionRoot;
                }

                SetPath(definitionRoot, row.LogicalPath, node);
                continue;
            }

            SetPath(root, row.LogicalPath, node);
        }

        return root.ToJsonString(JSON_OPTIONS);
    }

    private static IReadOnlyList<HistoryPathGroup> CollapseByPath(IReadOnlyList<ConfigurationValueHistory> rows)
    {
        return rows
            .GroupBy(
                row => $"{row.DefinitionKey}|{row.LogicalPath.ToCanonicalString()}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(row => row.ModifiedTime)
                    .ThenBy(row => row.Version)
                    .ToArray();
                return new HistoryPathGroup(ordered[0], ordered[^1]);
            })
            .OrderBy(group => group.Earliest.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Earliest.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        return node?.ToJsonString(JSON_OPTIONS) ?? "null";
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

    private sealed record HistoryPathGroup(ConfigurationValueHistory Earliest, ConfigurationValueHistory Latest);
}
