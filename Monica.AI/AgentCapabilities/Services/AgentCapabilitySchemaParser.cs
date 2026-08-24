using System.Text.Json;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Services;

internal static class AgentCapabilitySchemaParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Indented System.Text.Json defaults to Environment.NewLine; deterministic
        // artifacts (skill packs, snapshots) must not vary by operating system.
        NewLine = "\n"
    };

    internal static string? FormatSchema(JsonElement? schema)
    {
        if (schema is null || schema.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return JsonSerializer.Serialize(schema.Value, JsonOptions);
    }

    internal static IReadOnlyList<AgentCapabilityToolParameterInfo> ParseParameters(JsonElement? schema)
    {
        if (schema is null || schema.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var required = ReadRequired(schema.Value);
        if (!schema.Value.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var parameters = new List<AgentCapabilityToolParameterInfo>();
        foreach (var property in properties.EnumerateObject())
        {
            var parameterSchema = property.Value;
            parameters.Add(new AgentCapabilityToolParameterInfo(
                property.Name,
                ResolveType(parameterSchema),
                TryReadString(parameterSchema, "description"),
                required.Contains(property.Name),
                TryFormatProperty(parameterSchema, "default")));
        }

        return parameters;
    }

    private static HashSet<string> ReadRequired(JsonElement schema)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (!schema.TryGetProperty("required", out var requiredElement)
            || requiredElement.ValueKind != JsonValueKind.Array)
        {
            return required;
        }

        foreach (var item in requiredElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var name = item.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    required.Add(name);
                }
            }
        }

        return required;
    }

    private static string ResolveType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var typeElement))
        {
            return typeElement.ValueKind switch
            {
                JsonValueKind.String => typeElement.GetString() ?? "unknown",
                JsonValueKind.Array => string.Join(
                    " | ",
                    typeElement.EnumerateArray()
                        .Where(static item => item.ValueKind == JsonValueKind.String)
                        .Select(static item => item.GetString())
                        .Where(static type => !string.IsNullOrWhiteSpace(type))),
                _ => "unknown"
            };
        }

        if (schema.TryGetProperty("enum", out _))
        {
            return "enum";
        }

        if (schema.TryGetProperty("anyOf", out _))
        {
            return "anyOf";
        }

        if (schema.TryGetProperty("oneOf", out _))
        {
            return "oneOf";
        }

        return "unknown";
    }

    private static string? TryReadString(JsonElement schema, string propertyName)
    {
        return schema.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? TryFormatProperty(JsonElement schema, string propertyName)
    {
        if (!schema.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.GetRawText();
    }
}
