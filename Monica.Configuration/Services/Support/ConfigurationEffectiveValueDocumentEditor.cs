using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

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
            ConfigurationMutationKind.Replace => patchEngine.Patch(
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
        if (logicalPath.Depth == 0)
        {
            return ConfigurationStoredValue.FromJson(root.ToJsonString());
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

        return ConfigurationStoredValue.FromJson(current.ToJsonString());
    }

    /// <summary>
    /// Projects a complete effective value document into flat Microsoft configuration keys.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Project(ConfigurationDefinition definition, string json)
    {
        return codec.ToConfigurationValues(definition.SectionPath, ConfigurationStoredValue.FromJson(NormalizeJson(json)));
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
