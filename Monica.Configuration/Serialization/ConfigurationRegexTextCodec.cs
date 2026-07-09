using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Monica.Configuration.Models;
using Monica.Tool.Text;

namespace Monica.Configuration.Serialization;

/// <summary>
/// Normalizes configuration text that is known to represent a regular expression pattern.
/// </summary>
public static class ConfigurationRegexTextCodec
{
    private const int REGEX_VALIDATION_TIMEOUT_MILLISECONDS = 1000;

    /// <summary>
    /// Normalizes a regular expression pattern so non-ASCII UTF-16 code units use uppercase <c>\uXXXX</c> escapes.
    /// Existing ASCII regex syntax is left unchanged.
    /// </summary>
    /// <param name="pattern">The pattern to normalize.</param>
    /// <returns>The normalized pattern.</returns>
    public static string NormalizePattern(string? pattern)
    {
        return string.IsNullOrEmpty(pattern) ? string.Empty : pattern.ToRegexUnicodeEscaped();
    }

    /// <summary>
    /// Normalizes a display value when the schema marks it as regular expression pattern text.
    /// </summary>
    /// <param name="schema">The schema node that owns the value.</param>
    /// <param name="displayValue">The display value to normalize.</param>
    /// <returns>The normalized display value.</returns>
    public static string NormalizeDisplayValue(ConfigurationNodeDefinition schema, string? displayValue)
    {
        displayValue ??= string.Empty;
        return schema.IsRegexPatternText ? NormalizePattern(displayValue) : displayValue;
    }

    /// <summary>
    /// Normalizes a stored scalar JSON value when the schema marks it as regular expression pattern text.
    /// </summary>
    /// <param name="schema">The schema node that owns the value.</param>
    /// <param name="value">The stored JSON value.</param>
    /// <returns>The normalized stored JSON value.</returns>
    public static ConfigurationStoredValue NormalizeStoredValue(
        ConfigurationNodeDefinition schema,
        ConfigurationStoredValue value)
    {
        if (!ContainsRegexPatternText(schema))
        {
            return value;
        }

        try
        {
            var normalized = NormalizeJsonNode(schema, JsonNode.Parse(value.Json));
            return ConfigurationStoredValue.FromJson(normalized?.ToJsonString() ?? "null");
        }
        catch (JsonException)
        {
            return value;
        }
    }

    /// <summary>
    /// Returns a cloned JSON tree with every regex-pattern scalar normalized according to the supplied schema.
    /// </summary>
    /// <param name="schema">The schema describing the JSON tree.</param>
    /// <param name="node">The JSON node to clone and normalize.</param>
    /// <returns>A normalized clone, or <see langword="null"/> when <paramref name="node"/> is null.</returns>
    public static JsonNode? NormalizeJsonNode(ConfigurationNodeDefinition schema, JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (schema.IsRegexPatternText)
        {
            return TryReadString(node) is { } pattern
                ? JsonValue.Create(NormalizePattern(pattern))
                : node.DeepClone();
        }

        var clone = node.DeepClone();
        switch (clone)
        {
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Object:
                NormalizeObject(jsonObject, schema);
                break;
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Dictionary
                                      && schema.DictionaryTemplate is { } dictionaryTemplate:
                NormalizeDictionary(jsonObject, dictionaryTemplate.ValueTemplate);
                break;
            case JsonArray jsonArray when schema.NodeKind == ConfigurationNodeKind.List
                                    && schema.ListTemplate is { } listTemplate:
                NormalizeList(jsonArray, listTemplate.ItemTemplate);
                break;
        }

        return clone;
    }

    /// <summary>
    /// Decodes regex Unicode escape sequences in simple regex-derived display values.
    /// </summary>
    /// <param name="value">The value to decode.</param>
    /// <returns>The value with <c>\uXXXX</c> sequences converted to their UTF-16 code units.</returns>
    public static string DecodeRegexUnicodeEscapes(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (IsUnicodeEscapeAt(value, index))
            {
                var hex = value.Substring(index + 2, 4);
                result.Append((char)Convert.ToInt32(hex, 16));
                index += 5;
                continue;
            }

            result.Append(value[index]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks whether the supplied pattern can be parsed by the .NET regular expression engine.
    /// </summary>
    /// <param name="pattern">The regex pattern to validate.</param>
    /// <param name="errorMessage">The parser error message when validation fails.</param>
    /// <returns><see langword="true"/> when the pattern is empty or valid; otherwise <see langword="false"/>.</returns>
    public static bool TryValidatePattern(string? pattern, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            errorMessage = null;
            return true;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(REGEX_VALIDATION_TIMEOUT_MILLISECONDS));
            errorMessage = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void NormalizeObject(JsonObject jsonObject, ConfigurationNodeDefinition schema)
    {
        foreach (var child in schema.Children)
        {
            if (jsonObject[child.Name] is { } childNode)
            {
                jsonObject[child.Name] = NormalizeJsonNode(child, childNode);
            }
        }
    }

    private static void NormalizeDictionary(JsonObject jsonObject, ConfigurationNodeDefinition valueSchema)
    {
        foreach (var pair in jsonObject.ToArray())
        {
            jsonObject[pair.Key] = NormalizeJsonNode(valueSchema, pair.Value);
        }
    }

    private static void NormalizeList(JsonArray jsonArray, ConfigurationNodeDefinition itemSchema)
    {
        for (var index = 0; index < jsonArray.Count; index++)
        {
            jsonArray[index] = NormalizeJsonNode(itemSchema, jsonArray[index]);
        }
    }

    private static string? TryReadString(JsonNode node)
    {
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsUnicodeEscapeAt(string value, int index)
    {
        return index + 5 < value.Length
               && value[index] == '\\'
               && value[index + 1] == 'u'
               && IsHex(value[index + 2])
               && IsHex(value[index + 3])
               && IsHex(value[index + 4])
               && IsHex(value[index + 5]);
    }

    private static bool IsHex(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private static bool ContainsRegexPatternText(ConfigurationNodeDefinition schema)
    {
        if (schema.IsRegexPatternText)
        {
            return true;
        }

        return schema.Children.Any(ContainsRegexPatternText)
               || schema.DictionaryTemplate is { } dictionaryTemplate
                  && ContainsRegexPatternText(dictionaryTemplate.ValueTemplate)
               || schema.ListTemplate is { } listTemplate
                  && ContainsRegexPatternText(listTemplate.ItemTemplate);
    }
}
