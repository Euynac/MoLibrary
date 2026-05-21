using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Reads and edits effective value JSON documents by Monica logical path.
/// </summary>
public sealed class ConfigurationEffectiveValueDocumentEditor(
    ConfigurationContainerSnapshotEditor snapshotEditor,
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
                : RequirePlainJson(request.Value, request.LogicalPath);
        }

        var currentValue = ConfigurationStoredValue.Plain(NormalizeJson(currentJson));
        var updated = request.MutationKind switch
        {
            ConfigurationMutationKind.Set => snapshotEditor.Patch(
                currentValue,
                definition,
                LogicalPath.Root,
                request.LogicalPath,
                request.Value),
            ConfigurationMutationKind.Replace => snapshotEditor.Patch(
                currentValue,
                definition,
                LogicalPath.Root,
                request.LogicalPath,
                request.Value),
            ConfigurationMutationKind.Remove => snapshotEditor.Remove(
                currentValue,
                definition,
                LogicalPath.Root,
                request.LogicalPath),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.MutationKind, "Unsupported mutation kind.")
        };

        return NormalizeJson(updated.PlainJson ?? "{}");
    }

    /// <summary>
    /// Reads one logical path from a complete effective value document.
    /// </summary>
    public ConfigurationStoredValue? ReadValue(ConfigurationDefinition definition, string json, LogicalPath logicalPath)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject();
        if (logicalPath.Depth == 0)
        {
            return ConfigurationStoredValue.Plain(root.ToJsonString());
        }

        var current = root;
        var currentSchema = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            var childSchema = ResolveChildSchema(currentSchema, segment, logicalPath);
            current = GetExistingChild(current, currentSchema, segment);
            if (current is null)
            {
                return null;
            }

            currentSchema = childSchema;
        }

        return ConfigurationStoredValue.Plain(current.ToJsonString());
    }

    /// <summary>
    /// Projects a complete effective value document into flat Microsoft configuration keys.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Project(ConfigurationDefinition definition, string json)
    {
        return codec.ToConfigurationValues(definition.SectionPath, ConfigurationStoredValue.Plain(NormalizeJson(json)));
    }

    private static string RequirePlainJson(ConfigurationStoredValue value, LogicalPath logicalPath)
    {
        if (value.Kind != ConfigurationStoredValueKind.PlainJson || value.PlainJson is null)
        {
            throw new ConfigurationValidationFailedException(
                $"Value for '{logicalPath}' must be a plain JSON payload when editing an effective value document.");
        }

        return NormalizeJson(value.PlainJson);
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

    private static JsonNode? FindArrayItemByKey(JsonArray currentArray, ConfigurationNodeDefinition listSchema, string itemKey)
    {
        var keyPropertyName = listSchema.ListTemplate?.ItemKeyPropertyName;
        if (string.IsNullOrWhiteSpace(keyPropertyName))
        {
            return null;
        }

        return currentArray.FirstOrDefault(item =>
            item is JsonObject itemObject
            && itemObject.TryGetPropertyValue(keyPropertyName, out var value)
            && string.Equals(ReadScalarAsString(value), itemKey, StringComparison.OrdinalIgnoreCase));
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
