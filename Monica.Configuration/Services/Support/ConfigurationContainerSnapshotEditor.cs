using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Utils;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Edits plain JSON container snapshots by logical path.
/// </summary>
public sealed class ConfigurationContainerSnapshotEditor
{
    /// <summary>
    /// Replaces one descendant value inside a container snapshot.
    /// </summary>
    /// <param name="container">The container snapshot payload.</param>
    /// <param name="definition">The schema definition that owns the snapshot.</param>
    /// <param name="containerPath">The logical path represented by the snapshot.</param>
    /// <param name="targetPath">The descendant logical path to patch.</param>
    /// <param name="newValue">The replacement value payload.</param>
    /// <returns>The patched container snapshot payload.</returns>
    public ConfigurationStoredValue Patch(
        ConfigurationStoredValue container,
        ConfigurationDefinition definition,
        LogicalPath containerPath,
        LogicalPath targetPath,
        ConfigurationStoredValue newValue)
    {
        var root = ParseContainer(container, containerPath);
        var containerNode = ResolveNode(definition, containerPath);
        if (newValue.Kind != ConfigurationStoredValueKind.PlainJson || newValue.PlainJson is null)
        {
            throw new ConfigurationValidationFailedException(
                $"Value '{targetPath}' cannot patch container snapshot '{containerPath}' because it is not plain JSON.");
        }

        PatchNode(root, containerNode, GetRelativeSegments(containerPath, targetPath), JsonNode.Parse(newValue.PlainJson));
        return ConfigurationStoredValue.Plain(root.ToJsonString());
    }

    /// <summary>
    /// Removes one descendant value from a container snapshot.
    /// </summary>
    /// <param name="container">The container snapshot payload.</param>
    /// <param name="definition">The schema definition that owns the snapshot.</param>
    /// <param name="containerPath">The logical path represented by the snapshot.</param>
    /// <param name="targetPath">The descendant logical path to remove.</param>
    /// <returns>The patched container snapshot payload.</returns>
    public ConfigurationStoredValue Remove(
        ConfigurationStoredValue container,
        ConfigurationDefinition definition,
        LogicalPath containerPath,
        LogicalPath targetPath)
    {
        var root = ParseContainer(container, containerPath);
        var containerNode = ResolveNode(definition, containerPath);
        RemoveNode(root, containerNode, GetRelativeSegments(containerPath, targetPath));
        return ConfigurationStoredValue.Plain(root.ToJsonString());
    }

    private static IReadOnlyList<ConfigurationPathSegment> GetRelativeSegments(LogicalPath containerPath, LogicalPath targetPath)
    {
        if (!ConfigurationPathTokenizer.StartsWith(targetPath, containerPath))
        {
            throw new ConfigurationValidationFailedException(
                $"Path '{targetPath}' is not inside container snapshot '{containerPath}'.");
        }

        return targetPath.Segments.Skip(containerPath.Depth).ToArray();
    }

    private static JsonNode ParseContainer(ConfigurationStoredValue container, LogicalPath containerPath)
    {
        if (container.Kind != ConfigurationStoredValueKind.PlainJson || container.PlainJson is null)
        {
            throw new ConfigurationValidationFailedException(
                $"Container snapshot '{containerPath}' cannot be patched because it is not plain JSON.");
        }

        return JsonNode.Parse(container.PlainJson) ?? new JsonObject();
    }

    private static ConfigurationNodeDefinition ResolveNode(ConfigurationDefinition definition, LogicalPath path)
    {
        var current = definition.Root;
        foreach (var segment in path.Segments)
        {
            current = ResolveChildSchema(current, segment, path);
        }

        return current;
    }

    private static void PatchNode(
        JsonNode root,
        ConfigurationNodeDefinition rootSchema,
        IReadOnlyList<ConfigurationPathSegment> segments,
        JsonNode? value)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Cannot patch an empty relative path.");
        }

        var current = root;
        var currentSchema = rootSchema;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var childSchema = ResolveChildSchema(currentSchema, segments[i]);
            current = GetOrCreateChild(current, currentSchema, segments[i], childSchema);
            currentSchema = childSchema;
        }

        SetChild(current, currentSchema, segments[^1], value);
    }

    private static void RemoveNode(
        JsonNode root,
        ConfigurationNodeDefinition rootSchema,
        IReadOnlyList<ConfigurationPathSegment> segments)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Cannot remove an empty relative path.");
        }

        var current = root;
        var currentSchema = rootSchema;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var childSchema = ResolveChildSchema(currentSchema, segments[i]);
            var child = GetExistingChild(current, currentSchema, segments[i]);
            if (child is null)
            {
                return;
            }

            current = child;
            currentSchema = childSchema;
        }

        RemoveChild(current, currentSchema, segments[^1]);
    }

    private static ConfigurationNodeDefinition ResolveChildSchema(
        ConfigurationNodeDefinition current,
        ConfigurationPathSegment segment,
        LogicalPath? fullPath = null)
    {
        var child = segment switch
        {
            PropertySegment property => current.Children.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
            DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
            ListIndexSegment => current.ListTemplate?.ItemTemplate,
            ListItemKeySegment => current.ListTemplate is { SupportsPerItemMutation: true } listTemplate
                ? listTemplate.ItemTemplate
                : null,
            _ => null
        };

        return child ?? throw new ConfigurationValidationFailedException(
            $"Path segment '{segment.Value}' does not exist in schema{FormatPathSuffix(fullPath)}.");
    }

    private static string FormatPathSuffix(LogicalPath? path)
    {
        return path is null ? string.Empty : $" for '{path}'";
    }

    private static JsonNode GetOrCreateChild(
        JsonNode current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment,
        ConfigurationNodeDefinition childSchema)
    {
        return current switch
        {
            JsonObject currentObject => GetOrCreateObjectChild(currentObject, segment, childSchema),
            JsonArray currentArray => GetOrCreateArrayChild(currentArray, currentSchema, segment, childSchema),
            _ => throw new ConfigurationValidationFailedException($"Cannot patch through JSON node '{segment.Value}'.")
        };
    }

    private static JsonNode? GetExistingChild(
        JsonNode current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment)
    {
        return current switch
        {
            JsonObject currentObject when segment is PropertySegment or DictionaryKeySegment => currentObject[segment.Value],
            JsonArray currentArray => GetExistingArrayChild(currentArray, currentSchema, segment),
            _ => null
        };
    }

    private static JsonNode GetOrCreateObjectChild(
        JsonObject currentObject,
        ConfigurationPathSegment segment,
        ConfigurationNodeDefinition childSchema)
    {
        if (segment is not PropertySegment and not DictionaryKeySegment)
        {
            throw new ConfigurationValidationFailedException($"Cannot patch object through segment '{segment.Value}'.");
        }

        return currentObject[segment.Value] ?? CreateAndAttachObjectChild(currentObject, segment.Value, childSchema);
    }

    private static JsonNode CreateAndAttachObjectChild(
        JsonObject currentObject,
        string propertyName,
        ConfigurationNodeDefinition childSchema)
    {
        var child = CreateTraversalNode(childSchema);
        currentObject[propertyName] = child;
        return child;
    }

    private static JsonNode GetOrCreateArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationPathSegment segment,
        ConfigurationNodeDefinition itemSchema)
    {
        switch (segment)
        {
            case ListIndexSegment listIndex:
                if (listIndex.Index < 0)
                {
                    throw new ConfigurationValidationFailedException($"List index '{listIndex.Index}' cannot be negative.");
                }

                EnsureArraySize(currentArray, listIndex.Index);
                return currentArray[listIndex.Index] ?? CreateAndAttachArrayChild(currentArray, listIndex.Index, itemSchema);
            case ListItemKeySegment itemKey:
                return FindArrayItemByKey(currentArray, listSchema, itemKey.ItemKey)
                       ?? AppendArrayItem(currentArray, listSchema, itemSchema, itemKey.ItemKey);
            default:
                throw new ConfigurationValidationFailedException($"Cannot patch array through segment '{segment.Value}'.");
        }
    }

    private static JsonNode CreateAndAttachArrayChild(JsonArray currentArray, int index, ConfigurationNodeDefinition itemSchema)
    {
        var child = CreateTraversalNode(itemSchema);
        currentArray[index] = child;
        return child;
    }

    private static JsonNode AppendArrayItem(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationNodeDefinition itemSchema,
        string itemKey)
    {
        var child = CreateTraversalNode(itemSchema);
        SetListItemKeyValue(child, listSchema, itemKey);
        currentArray.Add(child);
        return child;
    }

    private static JsonNode? GetExistingArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationPathSegment segment)
    {
        return segment switch
        {
            ListIndexSegment listIndex when listIndex.Index >= 0 && listIndex.Index < currentArray.Count => currentArray[listIndex.Index],
            ListItemKeySegment itemKey => FindArrayItemByKey(currentArray, listSchema, itemKey.ItemKey),
            _ => null
        };
    }

    private static void SetChild(
        JsonNode current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment,
        JsonNode? value)
    {
        switch (current)
        {
            case JsonObject currentObject when segment is PropertySegment or DictionaryKeySegment:
                currentObject[segment.Value] = value;
                break;
            case JsonArray currentArray:
                SetArrayChild(currentArray, currentSchema, segment, value);
                break;
            default:
                throw new ConfigurationValidationFailedException($"Cannot patch JSON node '{segment.Value}'.");
        }
    }

    private static void SetArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationPathSegment segment,
        JsonNode? value)
    {
        switch (segment)
        {
            case ListIndexSegment listIndex:
                if (listIndex.Index < 0)
                {
                    throw new ConfigurationValidationFailedException($"List index '{listIndex.Index}' cannot be negative.");
                }

                EnsureArraySize(currentArray, listIndex.Index);
                currentArray[listIndex.Index] = value;
                break;
            case ListItemKeySegment itemKey:
                SetKeyedArrayChild(currentArray, listSchema, itemKey.ItemKey, value);
                break;
            default:
                throw new ConfigurationValidationFailedException($"Cannot patch array through segment '{segment.Value}'.");
        }
    }

    private static void SetKeyedArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        string itemKey,
        JsonNode? value)
    {
        SetListItemKeyValue(value, listSchema, itemKey);
        var itemIndex = FindArrayItemIndexByKey(currentArray, listSchema, itemKey);
        if (itemIndex >= 0)
        {
            currentArray[itemIndex] = value;
            return;
        }

        currentArray.Add(value);
    }

    private static void RemoveChild(
        JsonNode current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment)
    {
        switch (current)
        {
            case JsonObject currentObject when segment is PropertySegment or DictionaryKeySegment:
                currentObject.Remove(segment.Value);
                break;
            case JsonArray currentArray:
                RemoveArrayChild(currentArray, currentSchema, segment);
                break;
        }
    }

    private static void RemoveArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationPathSegment segment)
    {
        switch (segment)
        {
            case ListIndexSegment listIndex when listIndex.Index >= 0 && listIndex.Index < currentArray.Count:
                currentArray.RemoveAt(listIndex.Index);
                break;
            case ListItemKeySegment itemKey:
                var itemIndex = FindArrayItemIndexByKey(currentArray, listSchema, itemKey.ItemKey);
                if (itemIndex >= 0)
                {
                    currentArray.RemoveAt(itemIndex);
                }

                break;
        }
    }

    private static JsonNode CreateTraversalNode(ConfigurationNodeDefinition schema)
    {
        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Object or ConfigurationNodeKind.Dictionary => new JsonObject(),
            ConfigurationNodeKind.List => new JsonArray(),
            _ => throw new ConfigurationValidationFailedException($"Cannot patch through scalar schema node '{schema.Name}'.")
        };
    }

    private static JsonNode? FindArrayItemByKey(JsonArray array, ConfigurationNodeDefinition listSchema, string itemKey)
    {
        var itemIndex = FindArrayItemIndexByKey(array, listSchema, itemKey);
        return itemIndex < 0 ? null : array[itemIndex];
    }

    private static int FindArrayItemIndexByKey(JsonArray array, ConfigurationNodeDefinition listSchema, string itemKey)
    {
        var keyPropertyName = GetListItemKeyPropertyName(listSchema);
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is JsonObject item
                && ScalarValueEquals(item[keyPropertyName], itemKey))
            {
                return index;
            }
        }

        return -1;
    }

    private static void SetListItemKeyValue(JsonNode? item, ConfigurationNodeDefinition listSchema, string itemKey)
    {
        var keyPropertyName = GetListItemKeyPropertyName(listSchema);
        if (item is not JsonObject itemObject)
        {
            throw new ConfigurationValidationFailedException(
                $"List item '{itemKey}' must be represented as a JSON object because '{keyPropertyName}' is its stable key property.");
        }

        itemObject[keyPropertyName] = JsonValue.Create(itemKey);
    }

    private static string GetListItemKeyPropertyName(ConfigurationNodeDefinition listSchema)
    {
        if (listSchema.ListTemplate is { SupportsPerItemMutation: true } listTemplate)
        {
            return listTemplate.ItemKeyPropertyName!;
        }

        throw new ConfigurationValidationFailedException(
            $"List node '{listSchema.Name}' does not define a stable item key property.");
    }

    private static bool ScalarValueEquals(JsonNode? value, string expected)
    {
        var actual = GetScalarString(value);
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string? GetScalarString(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static void EnsureArraySize(JsonArray array, int index)
    {
        while (array.Count <= index)
        {
            array.Add(null);
        }
    }
}
