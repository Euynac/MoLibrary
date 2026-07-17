using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationUnifiedVersionJsonFormatter
{
    public static ConfigurationUnifiedVersionJsonDisplayPair FormatComparisonForDisplay(
        string? originJson,
        string? targetJson,
        ConfigurationDefinition? definition,
        string sensitiveLabel,
        string hiddenLabel,
        string hiddenOriginChangeLabel,
        string hiddenTargetChangeLabel,
        bool redactAllScalars)
    {
        var origin = Parse(originJson);
        var target = Parse(targetJson);

        if (definition is null || !origin.IsValid || !target.IsValid)
        {
            return FormatFullyHiddenComparison(
                origin,
                target,
                hiddenLabel,
                hiddenOriginChangeLabel,
                hiddenTargetChangeLabel);
        }

        var hasHiddenChanges = false;
        var formattedOrigin = origin.Exists
            ? RedactNode(
                origin.Node,
                target.Node,
                target.Exists,
                definition.Root,
                sensitiveLabel,
                hiddenLabel,
                hiddenOriginChangeLabel,
                redactAllScalars,
                ref hasHiddenChanges)
            : null;
        var formattedTarget = target.Exists
            ? RedactNode(
                target.Node,
                origin.Node,
                origin.Exists,
                definition.Root,
                sensitiveLabel,
                hiddenLabel,
                hiddenTargetChangeLabel,
                redactAllScalars,
                ref hasHiddenChanges)
            : null;

        return new ConfigurationUnifiedVersionJsonDisplayPair(
            FormatNode(formattedOrigin, origin.Exists),
            FormatNode(formattedTarget, target.Exists),
            hasHiddenChanges);
    }

    private static ConfigurationUnifiedVersionJsonDisplayPair FormatFullyHiddenComparison(
        ParsedJson origin,
        ParsedJson target,
        string hiddenLabel,
        string hiddenOriginChangeLabel,
        string hiddenTargetChangeLabel)
    {
        var hasHiddenChanges = AreEquivalent(origin, target) is false;
        return new ConfigurationUnifiedVersionJsonDisplayPair(
            FormatFullyHiddenValue(origin, hasHiddenChanges ? hiddenOriginChangeLabel : hiddenLabel),
            FormatFullyHiddenValue(target, hasHiddenChanges ? hiddenTargetChangeLabel : hiddenLabel),
            hasHiddenChanges);
    }

    private static string FormatFullyHiddenValue(ParsedJson value, string label)
    {
        return value.Exists ? JsonSerializer.Serialize(label) : "null";
    }

    private static JsonNode? RedactNode(
        JsonNode? node,
        JsonNode? counterpart,
        bool counterpartExists,
        ConfigurationNodeDefinition schema,
        string sensitiveLabel,
        string hiddenLabel,
        string changedLabel,
        bool redactAllScalars,
        ref bool hasHiddenChanges)
    {
        if (schema.IsSensitive)
        {
            return HideNode(
                node,
                counterpart,
                counterpartExists,
                sensitiveLabel,
                changedLabel,
                ref hasHiddenChanges);
        }

        if (schema.NodeKind == ConfigurationNodeKind.Scalar && redactAllScalars)
        {
            return HideNode(
                node,
                counterpart,
                counterpartExists,
                hiddenLabel,
                changedLabel,
                ref hasHiddenChanges);
        }

        if (redactAllScalars && schema.NodeKind is ConfigurationNodeKind.Dictionary or ConfigurationNodeKind.List)
        {
            return HideNode(
                node,
                counterpart,
                counterpartExists,
                hiddenLabel,
                changedLabel,
                ref hasHiddenChanges);
        }

        if (redactAllScalars
            && schema.NodeKind == ConfigurationNodeKind.Object
            && (ContainsUnknownProperty(node as JsonObject, schema)
                || ContainsUnknownProperty(counterpart as JsonObject, schema)))
        {
            return HideNode(
                node,
                counterpart,
                counterpartExists,
                hiddenLabel,
                changedLabel,
                ref hasHiddenChanges);
        }

        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Object when node is JsonObject jsonObject =>
                RedactObject(
                    jsonObject,
                    counterpart as JsonObject,
                    schema,
                    sensitiveLabel,
                    hiddenLabel,
                    changedLabel,
                    redactAllScalars,
                    ref hasHiddenChanges),
            ConfigurationNodeKind.Dictionary when node is JsonObject dictionary
                                                  && schema.DictionaryTemplate is not null =>
                RedactDictionary(
                    dictionary,
                    counterpart as JsonObject,
                    schema.DictionaryTemplate.ValueTemplate,
                    sensitiveLabel,
                    hiddenLabel,
                    changedLabel,
                    redactAllScalars,
                    ref hasHiddenChanges),
            ConfigurationNodeKind.List when node is JsonArray array
                                            && schema.ListTemplate is not null =>
                RedactList(
                    array,
                    counterpart as JsonArray,
                    schema.ListTemplate.ItemTemplate,
                    sensitiveLabel,
                    hiddenLabel,
                    changedLabel,
                    redactAllScalars,
                    ref hasHiddenChanges),
            ConfigurationNodeKind.Scalar when node is JsonValue or null => node?.DeepClone(),
            _ => HideNode(
                node,
                counterpart,
                counterpartExists,
                hiddenLabel,
                changedLabel,
                ref hasHiddenChanges)
        };
    }

    private static JsonObject RedactObject(
        JsonObject jsonObject,
        JsonObject? counterpart,
        ConfigurationNodeDefinition schema,
        string sensitiveLabel,
        string hiddenLabel,
        string changedLabel,
        bool redactAllScalars,
        ref bool hasHiddenChanges)
    {
        var result = new JsonObject();
        foreach (var property in jsonObject)
        {
            var counterpartExists = TryGetProperty(counterpart, property.Key, out var counterpartValue);
            var child = schema.Children.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, property.Key, StringComparison.OrdinalIgnoreCase));

            result[property.Key] = child is null
                ? HideNode(
                    property.Value,
                    counterpartValue,
                    counterpartExists,
                    hiddenLabel,
                    changedLabel,
                    ref hasHiddenChanges)
                : RedactNode(
                    property.Value,
                    counterpartValue,
                    counterpartExists,
                    child,
                    sensitiveLabel,
                    hiddenLabel,
                    changedLabel,
                    redactAllScalars,
                    ref hasHiddenChanges);
        }

        return result;
    }

    private static bool ContainsUnknownProperty(
        JsonObject? jsonObject,
        ConfigurationNodeDefinition schema)
    {
        return jsonObject is not null
               && jsonObject.Any(property => schema.Children.All(child =>
                   !string.Equals(child.Name, property.Key, StringComparison.OrdinalIgnoreCase)));
    }

    private static JsonObject RedactDictionary(
        JsonObject dictionary,
        JsonObject? counterpart,
        ConfigurationNodeDefinition valueSchema,
        string sensitiveLabel,
        string hiddenLabel,
        string changedLabel,
        bool redactAllScalars,
        ref bool hasHiddenChanges)
    {
        var result = new JsonObject();
        foreach (var property in dictionary)
        {
            var counterpartExists = TryGetProperty(counterpart, property.Key, out var counterpartValue);
            result[property.Key] = RedactNode(
                property.Value,
                counterpartValue,
                counterpartExists,
                valueSchema,
                sensitiveLabel,
                hiddenLabel,
                changedLabel,
                redactAllScalars,
                ref hasHiddenChanges);
        }

        return result;
    }

    private static JsonArray RedactList(
        JsonArray array,
        JsonArray? counterpart,
        ConfigurationNodeDefinition itemSchema,
        string sensitiveLabel,
        string hiddenLabel,
        string changedLabel,
        bool redactAllScalars,
        ref bool hasHiddenChanges)
    {
        var result = new JsonArray();
        for (var index = 0; index < array.Count; index++)
        {
            var counterpartExists = counterpart is not null && index < counterpart.Count;
            result.Add(RedactNode(
                array[index],
                counterpartExists ? counterpart![index] : null,
                counterpartExists,
                itemSchema,
                sensitiveLabel,
                hiddenLabel,
                changedLabel,
                redactAllScalars,
                ref hasHiddenChanges));
        }

        return result;
    }

    private static JsonNode HideNode(
        JsonNode? node,
        JsonNode? counterpart,
        bool counterpartExists,
        string hiddenLabel,
        string changedLabel,
        ref bool hasHiddenChanges)
    {
        var changed = !AreEquivalent(node, true, counterpart, counterpartExists);
        hasHiddenChanges |= changed;
        return JsonValue.Create(changed ? changedLabel : hiddenLabel)!;
    }

    private static bool TryGetProperty(JsonObject? jsonObject, string key, out JsonNode? value)
    {
        if (jsonObject is not null)
        {
            foreach (var property in jsonObject)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    private static bool AreEquivalent(ParsedJson left, ParsedJson right)
    {
        if (left.IsValid && right.IsValid)
        {
            return AreEquivalent(left.Node, left.Exists, right.Node, right.Exists);
        }

        return left.Exists == right.Exists
               && string.Equals(left.RawJson, right.RawJson, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(
        JsonNode? left,
        bool leftExists,
        JsonNode? right,
        bool rightExists)
    {
        if (leftExists != rightExists)
        {
            return false;
        }

        if (!leftExists || left is null || right is null)
        {
            return !leftExists || left is null && right is null;
        }

        if (left is JsonArray leftArray && right is JsonArray rightArray)
        {
            if (leftArray.Count != rightArray.Count)
            {
                return false;
            }

            for (var index = 0; index < leftArray.Count; index++)
            {
                if (!AreEquivalent(leftArray[index], true, rightArray[index], true))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is JsonObject leftObject && right is JsonObject rightObject)
        {
            if (leftObject.Count != rightObject.Count)
            {
                return false;
            }

            var unmatched = rightObject.ToList();
            foreach (var leftProperty in leftObject)
            {
                var matchIndex = unmatched.FindIndex(rightProperty =>
                    string.Equals(leftProperty.Key, rightProperty.Key, StringComparison.OrdinalIgnoreCase));
                if (matchIndex < 0
                    || !AreEquivalent(leftProperty.Value, true, unmatched[matchIndex].Value, true))
                {
                    return false;
                }

                unmatched.RemoveAt(matchIndex);
            }

            return true;
        }

        return left is JsonValue && right is JsonValue && JsonNode.DeepEquals(left, right);
    }

    private static ParsedJson Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ParsedJson(false, true, null, json);
        }

        try
        {
            return new ParsedJson(true, true, JsonNode.Parse(json), json);
        }
        catch (JsonException)
        {
            return new ParsedJson(true, false, null, json);
        }
    }

    private static string FormatNode(JsonNode? node, bool exists)
    {
        return exists
            ? node?.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions) ?? "null"
            : "null";
    }

    public static string VersionLabel(long version)
    {
        return string.Create(CultureInfo.InvariantCulture, $"v{version}");
    }

    private readonly record struct ParsedJson(bool Exists, bool IsValid, JsonNode? Node, string? RawJson);
}

internal sealed record ConfigurationUnifiedVersionJsonDisplayPair(
    string OriginText,
    string TargetText,
    bool HasHiddenChanges);
