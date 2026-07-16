using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Reads and edits effective value JSON documents by Monica logical path.
/// </summary>
public sealed class ConfigurationEffectiveValueDocumentEditor(
    ConfigurationEffectiveValuePatchEngine patchEngine,
    ConfigurationStoredValueCodec codec)
{
    /// <summary>
    /// Applies one mutation to a complete effective value document.
    /// </summary>
    public string ApplyMutation(ConfigurationDefinition definition, string currentJson, ConfigurationMutationRequest request)
    {
        if (request.LogicalPath.Depth == 0)
        {
            return request.MutationKind == ConfigurationMutationKind.Remove
                ? "{}"
                : RequireJson(request.Value);
        }

        var updated = request.MutationKind switch
        {
            ConfigurationMutationKind.Set => patchEngine.Patch(
                NormalizeJson(currentJson),
                definition,
                request.LogicalPath,
                RequireJson(request.Value)),
            ConfigurationMutationKind.Remove => patchEngine.Remove(
                NormalizeJson(currentJson),
                definition,
                request.LogicalPath),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.MutationKind, "Unsupported mutation kind.")
        };

        return NormalizeJson(updated);
    }

    /// <summary>
    /// Reads one logical path from a complete effective value document.
    /// </summary>
    public ConfigurationStoredValue? ReadValue(ConfigurationDefinition definition, string json, LogicalPath logicalPath)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject();
        return ReadValue(definition, root, logicalPath);
    }

    /// <summary>
    /// Reads several logical paths after parsing the complete effective value document once.
    /// </summary>
    /// <param name="definition">The schema used to navigate the document.</param>
    /// <param name="json">The complete effective value document.</param>
    /// <param name="logicalPaths">The logical paths to read, in result order.</param>
    /// <returns>One stored value for each requested path.</returns>
    public IReadOnlyList<ConfigurationStoredValue?> ReadValues(
        ConfigurationDefinition definition,
        string json,
        IReadOnlyList<LogicalPath> logicalPaths)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject();
        var values = new ConfigurationStoredValue?[logicalPaths.Count];
        for (var index = 0; index < logicalPaths.Count; index++)
        {
            values[index] = ReadValue(definition, root, logicalPaths[index]);
        }

        return values;
    }

    private static ConfigurationStoredValue? ReadValue(
        ConfigurationDefinition definition,
        JsonNode root,
        LogicalPath logicalPath)
    {
        if (logicalPath.Depth == 0)
        {
            return ConfigurationStoredValue.FromJson(root.ToJsonString());
        }

        var current = root;
        var currentSchema = definition.Root;
        for (var segmentIndex = 0; segmentIndex < logicalPath.Segments.Count; segmentIndex++)
        {
            var segment = logicalPath.Segments[segmentIndex];
            var childSchema = ResolveChildSchema(currentSchema, segment, logicalPath);
            if (!TryGetExistingChild(current, currentSchema, segment, out var child))
            {
                return null;
            }

            if (child is null)
            {
                return segmentIndex == logicalPath.Segments.Count - 1
                    ? ConfigurationStoredValue.Null
                    : null;
            }

            current = child;
            currentSchema = childSchema;
        }

        return ConfigurationStoredValue.FromJson(current.ToJsonString());
    }

    /// <summary>
    /// Projects a complete effective value document into flat Microsoft configuration keys.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Project(ConfigurationDefinition definition, string json)
    {
        var value = ConfigurationStoredValue.FromJson(NormalizeJson(json));
        return codec.ToConfigurationValues(
            definition.SectionPath,
            ConfigurationRegexTextCodec.NormalizeStoredValue(definition.Root, value));
    }

    private static string RequireJson(ConfigurationStoredValue value)
    {
        return NormalizeJson(value.Json);
    }

    private static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.GetRawText();
    }

    private static ConfigurationNodeDefinition ResolveChildSchema(
        ConfigurationNodeDefinition current,
        ConfigurationPathSegment segment,
        LogicalPath fullPath)
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
            $"Path segment '{segment.Value}' does not exist in schema for '{fullPath}'.");
    }

    private static bool TryGetExistingChild(
        JsonNode current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment,
        out JsonNode? child)
    {
        switch (current)
        {
            case JsonObject currentObject when segment is PropertySegment or DictionaryKeySegment:
                return TryGetObjectValue(currentObject, segment.Value, out child);
            case JsonArray currentArray:
                return TryGetExistingArrayChild(currentArray, currentSchema, segment, out child);
            default:
                child = null;
                return false;
        }
    }

    private static bool TryGetExistingArrayChild(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        ConfigurationPathSegment segment,
        out JsonNode? child)
    {
        switch (segment)
        {
            case ListIndexSegment listIndex when listIndex.Index >= 0 && listIndex.Index < currentArray.Count:
                child = currentArray[listIndex.Index];
                return true;
            case ListItemKeySegment itemKey:
                return TryFindArrayItemByKey(currentArray, listSchema, itemKey.ItemKey, out child);
            default:
                child = null;
                return false;
        }
    }

    private static bool TryFindArrayItemByKey(
        JsonArray currentArray,
        ConfigurationNodeDefinition listSchema,
        string itemKey,
        out JsonNode? item)
    {
        var keyPropertyName = listSchema.ListTemplate?.ItemKeyPropertyName;
        if (string.IsNullOrWhiteSpace(keyPropertyName))
        {
            item = null;
            return false;
        }

        foreach (var candidate in currentArray)
        {
            if (candidate is JsonObject itemObject
                && TryGetObjectValue(itemObject, keyPropertyName, out var value)
                && string.Equals(ReadScalarAsString(value), itemKey, StringComparison.OrdinalIgnoreCase))
            {
                item = candidate;
                return true;
            }
        }

        item = null;
        return false;
    }

    private static bool TryGetObjectValue(JsonObject jsonObject, string propertyName, out JsonNode? value)
    {
        foreach (var property in jsonObject)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string? ReadScalarAsString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(node.ToJsonString());
        }
        catch (JsonException)
        {
            return node.ToJsonString();
        }
    }
}
