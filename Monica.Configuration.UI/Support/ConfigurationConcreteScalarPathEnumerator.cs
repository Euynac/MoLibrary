using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Enumerates concrete scalar paths from a configuration schema and its current effective JSON value.
/// </summary>
internal static class ConfigurationConcreteScalarPathEnumerator
{
    public static IReadOnlyList<LogicalPath> Enumerate(
        ConfigurationNodeDefinition rootSchema,
        string? effectiveJson)
    {
        var paths = new List<LogicalPath>();
        Visit(rootSchema, rootSchema.RelativePath, Parse(effectiveJson), paths);
        return paths
            .Distinct()
            .OrderBy(static path => path.ToCanonicalString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static void Visit(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? value,
        ICollection<LogicalPath> paths)
    {
        switch (schema.NodeKind)
        {
            case ConfigurationNodeKind.Scalar:
                paths.Add(path);
                return;
            case ConfigurationNodeKind.Object:
                VisitObject(schema, path, value as JsonObject, paths);
                return;
            case ConfigurationNodeKind.Dictionary:
                VisitDictionary(schema, path, value as JsonObject, paths);
                return;
            case ConfigurationNodeKind.List:
                VisitList(schema, path, value as JsonArray, paths);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(schema), schema.NodeKind, "Unknown configuration node kind.");
        }
    }

    private static void VisitObject(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonObject? value,
        ICollection<LogicalPath> paths)
    {
        foreach (var child in schema.Children)
        {
            Visit(
                child,
                path.Append(new PropertySegment(child.Name)),
                GetProperty(value, child.Name),
                paths);
        }
    }

    private static void VisitDictionary(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonObject? value,
        ICollection<LogicalPath> paths)
    {
        if (schema.DictionaryTemplate?.ValueTemplate is not { } valueSchema || value is null)
        {
            return;
        }

        foreach (var entry in value)
        {
            Visit(
                valueSchema,
                path.Append(new DictionaryKeySegment(entry.Key)),
                entry.Value,
                paths);
        }
    }

    private static void VisitList(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonArray? value,
        ICollection<LogicalPath> paths)
    {
        if (schema.ListTemplate is not { } template || value is null)
        {
            return;
        }

        for (var index = 0; index < value.Count; index++)
        {
            var item = value[index];
            var itemPath = template.SupportsPerItemMutation
                           && ReadScalarProperty(item, template.ItemKeyPropertyName!) is { Length: > 0 } itemKey
                ? path.Append(new ListItemKeySegment(itemKey))
                : path.Append(new ListIndexSegment(index));
            Visit(template.ItemTemplate, itemPath, item, paths);
        }
    }

    private static JsonNode? GetProperty(JsonObject? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var property in value)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? ReadScalarProperty(JsonNode? value, string propertyName)
    {
        if (value is not JsonObject jsonObject || GetProperty(jsonObject, propertyName) is not { } propertyValue)
        {
            return null;
        }

        using var document = JsonDocument.Parse(propertyValue.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static JsonNode? Parse(string? json)
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
}
