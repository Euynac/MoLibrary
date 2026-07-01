using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationUnifiedVersionJsonFormatter
{
    public static string FormatForDisplay(
        string? json,
        ConfigurationDefinition? definition,
        string redactedLabel)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "null";
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not null && definition is not null)
            {
                RedactNode(node, definition.Root, redactedLabel);
            }

            return node?.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions) ?? "null";
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static void RedactNode(JsonNode? node, ConfigurationNodeDefinition schema, string redactedLabel)
    {
        if (node is null)
        {
            return;
        }

        if (schema.IsSensitive && schema.NodeKind == ConfigurationNodeKind.Scalar)
        {
            ReplaceCurrentValue(node, redactedLabel);
            return;
        }

        switch (schema.NodeKind)
        {
            case ConfigurationNodeKind.Object when node is JsonObject jsonObject:
                foreach (var child in schema.Children)
                {
                    if (jsonObject[child.Name] is { } childNode)
                    {
                        RedactNode(childNode, child, redactedLabel);
                    }
                }

                break;
            case ConfigurationNodeKind.Dictionary when node is JsonObject dictionary
                                                && schema.DictionaryTemplate is not null:
                foreach (var key in dictionary.Select(static pair => pair.Key).ToArray())
                {
                    if (dictionary[key] is { } value)
                    {
                        RedactNode(value, schema.DictionaryTemplate.ValueTemplate, redactedLabel);
                    }
                }

                break;
            case ConfigurationNodeKind.List when node is JsonArray array
                                      && schema.ListTemplate is not null:
                foreach (var item in array)
                {
                    RedactNode(item, schema.ListTemplate.ItemTemplate, redactedLabel);
                }

                break;
        }
    }

    private static void ReplaceCurrentValue(JsonNode node, string redactedLabel)
    {
        var parent = node.Parent;
        if (parent is JsonObject parentObject)
        {
            var key = parentObject.FirstOrDefault(pair => ReferenceEquals(pair.Value, node)).Key;
            if (!string.IsNullOrWhiteSpace(key))
            {
                parentObject[key] = redactedLabel;
            }

            return;
        }

        if (parent is JsonArray parentArray)
        {
            for (var i = 0; i < parentArray.Count; i++)
            {
                if (ReferenceEquals(parentArray[i], node))
                {
                    parentArray[i] = redactedLabel;
                    return;
                }
            }
        }
    }

    public static string VersionLabel(long version)
    {
        return string.Create(CultureInfo.InvariantCulture, $"v{version}");
    }
}
