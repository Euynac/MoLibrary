using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationJsonDisplayFormatter
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    public static string Format(JsonNode? node, ConfigurationNodeDefinition schema, string redactedLabel)
    {
        var redacted = RedactSensitive(CloneNode(node), schema, redactedLabel);
        return redacted?.ToJsonString(JSON_OPTIONS) ?? "null";
    }

    public static string Format(string? json, ConfigurationNodeDefinition schema, string redactedLabel)
    {
        return Format(ParseJson(json), schema, redactedLabel);
    }

    private static JsonNode? RedactSensitive(JsonNode? node, ConfigurationNodeDefinition schema, string redactedLabel)
    {
        if (node is null)
        {
            return null;
        }

        if (schema.IsSensitive)
        {
            return JsonValue.Create(redactedLabel);
        }

        switch (node)
        {
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Object:
                RedactObject(jsonObject, schema, redactedLabel);
                break;
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Dictionary && schema.DictionaryTemplate is not null:
                RedactDictionary(jsonObject, schema.DictionaryTemplate.ValueTemplate, redactedLabel);
                break;
            case JsonArray jsonArray when schema.NodeKind == ConfigurationNodeKind.List && schema.ListTemplate is not null:
                RedactList(jsonArray, schema.ListTemplate.ItemTemplate, redactedLabel);
                break;
        }

        return node;
    }

    private static void RedactObject(JsonObject jsonObject, ConfigurationNodeDefinition schema, string redactedLabel)
    {
        foreach (var child in schema.Children)
        {
            var current = jsonObject[child.Name];
            var redacted = RedactSensitive(current, child, redactedLabel);
            if (!ReferenceEquals(current, redacted))
            {
                jsonObject[child.Name] = redacted;
            }
        }
    }

    private static void RedactDictionary(JsonObject jsonObject, ConfigurationNodeDefinition valueSchema, string redactedLabel)
    {
        foreach (var pair in jsonObject.ToArray())
        {
            var redacted = RedactSensitive(pair.Value, valueSchema, redactedLabel);
            if (!ReferenceEquals(pair.Value, redacted))
            {
                jsonObject[pair.Key] = redacted;
            }
        }
    }

    private static void RedactList(JsonArray jsonArray, ConfigurationNodeDefinition itemSchema, string redactedLabel)
    {
        for (var index = 0; index < jsonArray.Count; index++)
        {
            var current = jsonArray[index];
            var redacted = RedactSensitive(current, itemSchema, redactedLabel);
            if (!ReferenceEquals(current, redacted))
            {
                jsonArray[index] = redacted;
            }
        }
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString());
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
            return JsonValue.Create(json);
        }
    }
}
