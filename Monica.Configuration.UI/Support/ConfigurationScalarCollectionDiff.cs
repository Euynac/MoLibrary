using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationScalarCollectionDiff
{
    public static IReadOnlyList<ConfigurationScalarListDiffItem> BuildListItems(
        string? currentJson,
        string? originalJson,
        ConfigurationNodeDefinition itemNode,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        var originalEntries = ReadListEntries(originalJson, itemNode, localizer);
        var currentEntries = ReadListEntries(currentJson, itemNode, localizer);
        return BuildListItems(currentEntries, originalEntries);
    }

    public static IReadOnlyList<ConfigurationScalarDictionaryDiffItem> BuildDictionaryItems(
        string? currentJson,
        string? originalJson,
        ConfigurationNodeDefinition valueNode,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        var originalEntries = ReadDictionaryEntries(originalJson, valueNode, localizer);
        var currentEntries = ReadDictionaryEntries(currentJson, valueNode, localizer);
        var originalByKey = originalEntries.ToDictionary(static entry => entry.Key, StringComparer.Ordinal);
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ConfigurationScalarDictionaryDiffItem>();

        foreach (var entry in currentEntries)
        {
            currentKeys.Add(entry.Key);
            result.Add(originalByKey.TryGetValue(entry.Key, out var originalEntry)
                ? new ConfigurationScalarDictionaryDiffItem(entry.Key, entry.DisplayValue, originalEntry.Key, originalEntry.StoredJson, IsDeleted: false)
                : new ConfigurationScalarDictionaryDiffItem(entry.Key, entry.DisplayValue, OriginalKey: null, OriginalStoredJson: null, IsDeleted: false));
        }

        foreach (var originalEntry in originalEntries.Where(entry => !currentKeys.Contains(entry.Key)))
        {
            result.Add(new ConfigurationScalarDictionaryDiffItem(
                originalEntry.Key,
                originalEntry.DisplayValue,
                originalEntry.Key,
                originalEntry.StoredJson,
                IsDeleted: true));
        }

        return result;
    }

    public static string? TryConvertDisplayValue(
        ConfigurationNodeDefinition node,
        string value,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        var conversion = ConfigurationScalarValueCodec.ConvertDisplayValue(node, value, localizer);
        return conversion.IsValid ? conversion.StoredValue?.Json : null;
    }

    public static bool StoredJsonEquals(string left, string right)
    {
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
        }
        catch (JsonException)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<ConfigurationScalarListDiffItem> BuildListItems(
        IReadOnlyList<ScalarCollectionEntry> currentEntries,
        IReadOnlyList<ScalarCollectionEntry> originalEntries)
    {
        var result = new List<ConfigurationScalarListDiffItem>();
        var matches = BuildListMatches(originalEntries, currentEntries);
        var originalCursor = 0;
        var currentCursor = 0;

        foreach (var match in matches)
        {
            AddListGapItems(result, currentEntries, originalEntries, originalCursor, match.OriginalIndex, currentCursor, match.CurrentIndex);
            result.Add(CreateCurrentListItem(currentEntries[match.CurrentIndex], originalEntries[match.OriginalIndex]));
            originalCursor = match.OriginalIndex + 1;
            currentCursor = match.CurrentIndex + 1;
        }

        AddListGapItems(result, currentEntries, originalEntries, originalCursor, originalEntries.Count, currentCursor, currentEntries.Count);
        return result;
    }

    private static void AddListGapItems(
        List<ConfigurationScalarListDiffItem> result,
        IReadOnlyList<ScalarCollectionEntry> currentEntries,
        IReadOnlyList<ScalarCollectionEntry> originalEntries,
        int originalStart,
        int originalEnd,
        int currentStart,
        int currentEnd)
    {
        // Equal-size gaps are treated as item edits; remaining current entries are additions and remaining original entries are deletions.
        var pairedCount = Math.Min(originalEnd - originalStart, currentEnd - currentStart);
        for (var offset = 0; offset < pairedCount; offset++)
        {
            result.Add(CreateCurrentListItem(
                currentEntries[currentStart + offset],
                originalEntries[originalStart + offset]));
        }

        for (var currentIndex = currentStart + pairedCount; currentIndex < currentEnd; currentIndex++)
        {
            result.Add(new ConfigurationScalarListDiffItem(currentEntries[currentIndex].DisplayValue, OriginalStoredJson: null, IsDeleted: false));
        }

        for (var originalIndex = originalStart + pairedCount; originalIndex < originalEnd; originalIndex++)
        {
            var originalEntry = originalEntries[originalIndex];
            result.Add(new ConfigurationScalarListDiffItem(originalEntry.DisplayValue, originalEntry.StoredJson, IsDeleted: true));
        }
    }

    private static ConfigurationScalarListDiffItem CreateCurrentListItem(
        ScalarCollectionEntry currentEntry,
        ScalarCollectionEntry originalEntry)
    {
        return new ConfigurationScalarListDiffItem(currentEntry.DisplayValue, originalEntry.StoredJson, IsDeleted: false);
    }

    private static IReadOnlyList<ListMatch> BuildListMatches(
        IReadOnlyList<ScalarCollectionEntry> originalEntries,
        IReadOnlyList<ScalarCollectionEntry> currentEntries)
    {
        var lengths = new int[originalEntries.Count + 1, currentEntries.Count + 1];
        for (var originalIndex = originalEntries.Count - 1; originalIndex >= 0; originalIndex--)
        {
            for (var currentIndex = currentEntries.Count - 1; currentIndex >= 0; currentIndex--)
            {
                lengths[originalIndex, currentIndex] = EntriesHaveSameStoredJson(originalEntries[originalIndex], currentEntries[currentIndex])
                    ? lengths[originalIndex + 1, currentIndex + 1] + 1
                    : Math.Max(lengths[originalIndex + 1, currentIndex], lengths[originalIndex, currentIndex + 1]);
            }
        }

        var matches = new List<ListMatch>();
        var i = 0;
        var j = 0;
        while (i < originalEntries.Count && j < currentEntries.Count)
        {
            if (EntriesHaveSameStoredJson(originalEntries[i], currentEntries[j]))
            {
                matches.Add(new ListMatch(i, j));
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return matches;
    }

    private static IReadOnlyList<ScalarCollectionEntry> ReadListEntries(
        string? json,
        ConfigurationNodeDefinition itemNode,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        if (ParseJson(json) is not JsonArray array)
        {
            return [];
        }

        return array.Select(item =>
        {
            var displayValue = ReadScalarDisplayValue(item, itemNode);
            return new ScalarCollectionEntry(displayValue, TryConvertDisplayValue(itemNode, displayValue, localizer));
        }).ToList();
    }

    private static IReadOnlyList<ScalarDictionaryEntry> ReadDictionaryEntries(
        string? json,
        ConfigurationNodeDefinition valueNode,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        if (ParseJson(json) is not JsonObject jsonObject)
        {
            return [];
        }

        return jsonObject.Select(pair =>
        {
            var displayValue = ReadScalarDisplayValue(pair.Value, valueNode);
            return new ScalarDictionaryEntry(pair.Key, displayValue, TryConvertDisplayValue(valueNode, displayValue, localizer));
        }).ToList();
    }

    private static bool EntriesHaveSameStoredJson(ScalarCollectionEntry left, ScalarCollectionEntry right)
    {
        return left.StoredJson is not null
               && right.StoredJson is not null
               && StoredJsonEquals(left.StoredJson, right.StoredJson);
    }

    private static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadScalarDisplayValue(JsonNode? node, ConfigurationNodeDefinition schema)
    {
        if (node is null)
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(node.ToJsonString());
        var value = document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString() ?? string.Empty,
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => document.RootElement.GetRawText()
        };
        return ConfigurationScalarValueCodec.NormalizeDisplayValue(schema, value);
    }

    private sealed record ScalarCollectionEntry(string DisplayValue, string? StoredJson);

    private sealed record ScalarDictionaryEntry(string Key, string DisplayValue, string? StoredJson);

    private sealed record ListMatch(int OriginalIndex, int CurrentIndex);
}

internal sealed record ConfigurationScalarListDiffItem(string DisplayValue, string? OriginalStoredJson, bool IsDeleted);

internal sealed record ConfigurationScalarDictionaryDiffItem(
    string Key,
    string DisplayValue,
    string? OriginalKey,
    string? OriginalStoredJson,
    bool IsDeleted);
