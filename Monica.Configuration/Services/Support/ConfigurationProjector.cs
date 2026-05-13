using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using System.Text.Json.Nodes;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Projects merged values into flat Microsoft configuration key/value pairs.
/// </summary>
internal sealed class ConfigurationProjector(
    ConfigurationPathProjector pathProjector,
    ConfigurationStoredValueCodec codec,
    IConfigurationSensitiveValueProtector sensitiveValueProtector)
    : IConfigurationProjector
{
    /// <inheritdoc />
    public IReadOnlyList<ProjectedConfigurationKey> Project(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IReadOnlyList<MergedNodeValue> values)
    {
        var definitionsByKey = definitions.ToDictionary(x => x.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var listIndexMap = BuildListIndexMap(definitionsByKey, values);
        return values
            .Where(value => definitionsByKey.ContainsKey(value.DefinitionKey))
            .SelectMany(value =>
            {
                var definition = definitionsByKey[value.DefinitionKey];
                var configurationPath = value.Override.ConfigurationPath is not null && !ContainsListItemKey(value.LogicalPath)
                    ? value.Override.ConfigurationPath
                    : pathProjector.Project(
                        definition.SectionPath,
                        value.LogicalPath,
                        (listPath, itemKey) => ResolveListIndex(listIndexMap, value.DefinitionKey, listPath, itemKey));
                var storedValue = sensitiveValueProtector.Unprotect(value.Override.Value);
                return codec.ToConfigurationValues(configurationPath, storedValue).Select(projected => new ProjectedCandidate(
                    new ProjectedConfigurationKey
                {
                    Key = projected.Key,
                    Value = projected.Value
                },
                    value.SourcePriority,
                    value.LogicalPath.Depth));
            })
            .GroupBy(candidate => candidate.Key.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.SourcePriority)
                .ThenByDescending(candidate => candidate.LogicalPathDepth)
                .First()
                .Key)
            .ToArray();
    }

    private sealed record ProjectedCandidate(ProjectedConfigurationKey Key, int SourcePriority, int LogicalPathDepth);

    private static IReadOnlyDictionary<(string DefinitionKey, string ListPath), IReadOnlyDictionary<string, int>> BuildListIndexMap(
        IReadOnlyDictionary<string, ConfigurationDefinition> definitionsByKey,
        IReadOnlyList<MergedNodeValue> values)
    {
        var candidates = values
            .Where(value => definitionsByKey.ContainsKey(value.DefinitionKey))
            .SelectMany(value => EnumerateListItems(
                definitionsByKey[value.DefinitionKey],
                value.DefinitionKey,
                value.LogicalPath,
                value.Override.Value));

        return candidates
            .GroupBy(item => (item.DefinitionKey, ListPath: item.ListPath.ToCanonicalString()))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)BuildOrderedListItemKeys(group)
                    .Select((itemKey, index) => new { itemKey, index })
                    .ToDictionary(item => item.itemKey, item => item.index, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> BuildOrderedListItemKeys(
        IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey, bool FromSnapshot, int Order)> group)
    {
        var items = group.ToArray();
        var snapshotKeys = items
            .Where(item => item.FromSnapshot)
            .OrderBy(item => item.Order)
            .Select(item => item.ItemKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var snapshotKeySet = snapshotKeys.ToHashSet(StringComparer.Ordinal);
        var pathOnlyKeys = items
            .Where(item => !item.FromSnapshot && !snapshotKeySet.Contains(item.ItemKey))
            .Select(item => item.ItemKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(itemKey => itemKey, StringComparer.Ordinal);

        return [.. snapshotKeys, .. pathOnlyKeys];
    }

    private static IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey, bool FromSnapshot, int Order)> EnumerateListItems(
        ConfigurationDefinition definition,
        string definitionKey,
        LogicalPath logicalPath,
        ConfigurationStoredValue value)
    {
        for (var index = 0; index < logicalPath.Segments.Count; index++)
        {
            if (logicalPath.Segments[index] is not ListItemKeySegment itemKey)
            {
                continue;
            }

            yield return (
                definitionKey,
                new LogicalPath(logicalPath.Segments.Take(index).ToArray()),
                itemKey.ItemKey,
                false,
                0);
        }

        foreach (var item in EnumerateListItemsFromSnapshot(definition, definitionKey, logicalPath, value))
        {
            yield return item;
        }
    }

    private static IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey, bool FromSnapshot, int Order)> EnumerateListItemsFromSnapshot(
        ConfigurationDefinition definition,
        string definitionKey,
        LogicalPath logicalPath,
        ConfigurationStoredValue value)
    {
        if (value.Kind != ConfigurationStoredValueKind.PlainJson || value.PlainJson is null)
        {
            yield break;
        }

        var schema = TryResolveSchema(definition, logicalPath);
        if (schema is null)
        {
            yield break;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(value.PlainJson);
        }
        catch
        {
            yield break;
        }

        if (root is null)
        {
            yield break;
        }

        foreach (var item in EnumerateListItemsFromJson(definitionKey, logicalPath, schema, root))
        {
            yield return item;
        }
    }

    private static ConfigurationNodeDefinition? TryResolveSchema(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey, bool FromSnapshot, int Order)> EnumerateListItemsFromJson(
        string definitionKey,
        LogicalPath currentPath,
        ConfigurationNodeDefinition currentSchema,
        JsonNode currentNode)
    {
        if (currentSchema.ListTemplate is { SupportsPerItemMutation: true } listTemplate
            && currentNode is JsonArray array)
        {
            foreach (var item in EnumerateCurrentListItems(definitionKey, currentPath, listTemplate, array))
            {
                yield return item;
            }
        }

        foreach (var child in EnumerateChildNodes(currentPath, currentSchema, currentNode))
        {
            foreach (var item in EnumerateListItemsFromJson(definitionKey, child.Path, child.Schema, child.Node))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey, bool FromSnapshot, int Order)> EnumerateCurrentListItems(
        string definitionKey,
        LogicalPath listPath,
        ConfigurationListTemplate listTemplate,
        JsonArray array)
    {
        var itemKeyPropertyName = listTemplate.ItemKeyPropertyName!;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject item)
            {
                continue;
            }

            var itemKey = GetScalarString(item[itemKeyPropertyName]);
            if (!string.IsNullOrWhiteSpace(itemKey))
            {
                yield return (definitionKey, listPath, itemKey, true, index);
            }
        }
    }

    private static IEnumerable<(LogicalPath Path, ConfigurationNodeDefinition Schema, JsonNode Node)> EnumerateChildNodes(
        LogicalPath currentPath,
        ConfigurationNodeDefinition currentSchema,
        JsonNode currentNode)
    {
        if (currentNode is JsonObject currentObject)
        {
            foreach (var child in EnumerateObjectChildren(currentPath, currentSchema, currentObject))
            {
                yield return child;
            }
        }

        if (currentNode is JsonArray currentArray && currentSchema.ListTemplate is { } listTemplate)
        {
            for (var index = 0; index < currentArray.Count; index++)
            {
                if (currentArray[index] is { } item)
                {
                    yield return (currentPath.Append(new ListIndexSegment(index)), listTemplate.ItemTemplate, item);
                }
            }
        }
    }

    private static IEnumerable<(LogicalPath Path, ConfigurationNodeDefinition Schema, JsonNode Node)> EnumerateObjectChildren(
        LogicalPath currentPath,
        ConfigurationNodeDefinition currentSchema,
        JsonObject currentObject)
    {
        foreach (var childSchema in currentSchema.Children)
        {
            if (currentObject[childSchema.Name] is { } childNode)
            {
                yield return (currentPath.Append(new PropertySegment(childSchema.Name)), childSchema, childNode);
            }
        }

        if (currentSchema.DictionaryTemplate is { } dictionaryTemplate)
        {
            foreach (var property in currentObject)
            {
                if (property.Value is { } childNode)
                {
                    yield return (currentPath.Append(new DictionaryKeySegment(property.Key)), dictionaryTemplate.ValueTemplate, childNode);
                }
            }
        }
    }

    private static string? GetScalarString(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        using var document = System.Text.Json.JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => document.RootElement.GetString(),
            System.Text.Json.JsonValueKind.Number => document.RootElement.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool ContainsListItemKey(LogicalPath logicalPath)
    {
        return logicalPath.Segments.Any(segment => segment is ListItemKeySegment);
    }

    private static int ResolveListIndex(
        IReadOnlyDictionary<(string DefinitionKey, string ListPath), IReadOnlyDictionary<string, int>> listIndexMap,
        string definitionKey,
        LogicalPath listPath,
        string itemKey)
    {
        if (listIndexMap.TryGetValue((definitionKey, listPath.ToCanonicalString()), out var itemIndexes)
            && itemIndexes.TryGetValue(itemKey, out var index))
        {
            return index;
        }

        throw new ConfigurationValidationFailedException(
            $"List item key '{itemKey}' under '{listPath}' could not be resolved to a projected list index.");
    }
}
