using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Core.Results;

namespace Monica.Configuration.Api;

internal sealed partial class ConfigurationApiService(
    ConfigurationFacade facade,
    IConfigurationHistoryService historyService,
    ConfigurationValidationCoordinator validationCoordinator)
{
    private static readonly JsonSerializerOptions READABLE_JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions COMPACT_JSON_OPTIONS = new();
    private async Task<string?> LoadSourceRevisionOrNullAsync(string sourceKey)
    {
        var result = await facade.GetSourceRevisionAsync(sourceKey);
        return result.IsFailed(out _, out var revision) ? null : revision;
    }

    private async Task<IReadOnlyList<ConfigurationDefinition>> LoadDefinitionsAsync(string? definitionKey)
    {
        var summariesResult = await facade.GetDefinitionsAsync();
        if (summariesResult.IsFailed(out var summariesError, out var summaries))
        {
            throw new InvalidOperationException(summariesError.Message);
        }

        var filtered = string.IsNullOrWhiteSpace(definitionKey)
            ? summaries
            : summaries.Where(summary => string.Equals(summary.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase));

        var definitions = new List<ConfigurationDefinition>();
        foreach (var summary in filtered.OrderBy(static summary => summary.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            definitions.Add(await LoadDefinitionAsync(summary.DefinitionKey));
        }

        return definitions;
    }

    private async Task<ConfigurationDefinition> LoadDefinitionAsync(string definitionKey)
    {
        var result = await facade.GetDefinitionAsync(definitionKey);
        if (result.IsFailed(out var error, out var detail))
        {
            throw new InvalidOperationException(error.Message);
        }

        return detail.Definition;
    }

    private async Task<ConfigurationEffectiveValue> LoadEffectiveValueAsync(string definitionKey, LogicalPath logicalPath)
    {
        var result = await facade.GetEffectiveValueAsync(definitionKey, logicalPath);
        if (result.IsFailed(out var error, out var effectiveValue))
        {
            throw new InvalidOperationException(error.Message);
        }

        return effectiveValue;
    }

    private static ConfigurationStoredValue ToStoredValue(ConfigurationMutationGroupPublishChange change)
    {
        return change.MutationKind == ConfigurationMutationKind.Remove
            ? ConfigurationStoredValue.Null
            : ConfigurationStoredValue.FromJson(change.Value?.ToJsonString(COMPACT_JSON_OPTIONS) ?? "null");
    }

    private static ConfigurationMutationGroupPublishResult Rejected(
        ConfigurationMutationGroup? group,
        ConfigurationMutationGroupPublishChange? failedChange,
        string failureMessage)
    {
        return new ConfigurationMutationGroupPublishResult
        {
            Status = ConfigurationMutationGroupPublishStatus.Rejected,
            Group = group,
            FailedChange = failedChange,
            FailureMessage = failureMessage
        };
    }

    private static IEnumerable<ConfigurationNodeDefinition> EnumerateSchemaNodes(ConfigurationNodeDefinition node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateSchemaNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static ConfigurationNodeDefinition ResolveNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            } ?? throw new InvalidOperationException(
                $"Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
        }

        return current;
    }

    private static LogicalPath ParsePath(string? logicalPath)
    {
        return LogicalPath.Parse(logicalPath ?? string.Empty);
    }

    private static bool MatchesSearch(ConfigurationParameterRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(row.DefinitionKey, search)
               || Contains(row.DefinitionDisplayName, search)
               || Contains(row.Category, search)
               || Contains(row.LogicalPath, search)
               || Contains(row.ConfigurationPath, search)
               || Contains(row.NodeDisplayName, search)
               || Contains(row.Description, search);
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) is true;
    }

    private static ConfigurationReloadBehavior ResolveReloadBehavior(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition node)
    {
        return node.ReloadBehavior is null or ConfigurationReloadBehavior.Inherit
            ? definition.ReloadBehavior
            : node.ReloadBehavior.Value;
    }

    private static string DisplayName(ConfigurationNodeDefinition node)
    {
        return string.IsNullOrWhiteSpace(node.DisplayName) ? node.Name : node.DisplayName;
    }

    private static ConfigurationApiDiagnostic Error(string definitionKey, LogicalPath logicalPath, string message)
    {
        return new ConfigurationApiDiagnostic
        {
            Severity = ConfigurationApiDiagnosticSeverity.Error,
            DefinitionKey = definitionKey,
            LogicalPath = logicalPath.ToCanonicalString(),
            Message = message
        };
    }

    private static JsonNode? ParseEffectiveNode(ConfigurationNodeDefinition schema, string? displayValue)
    {
        if (string.IsNullOrWhiteSpace(displayValue))
        {
            return CreateEmptyNode(schema);
        }

        if (schema.NodeKind != ConfigurationNodeKind.Scalar || schema.ValueKind == ConfigurationValueKind.Json)
        {
            try
            {
                return JsonNode.Parse(displayValue);
            }
            catch (JsonException)
            {
                return schema.NodeKind == ConfigurationNodeKind.Scalar
                    ? JsonValue.Create(displayValue)
                    : CreateEmptyNode(schema);
            }
        }

        return schema.ValueKind switch
        {
            ConfigurationValueKind.Boolean when bool.TryParse(displayValue, out var value) => JsonValue.Create(value),
            ConfigurationValueKind.Integer when long.TryParse(displayValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => JsonValue.Create(value),
            ConfigurationValueKind.Decimal when decimal.TryParse(displayValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) => JsonValue.Create(value),
            ConfigurationValueKind.Floating when double.TryParse(displayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => JsonValue.Create(value),
            _ => JsonValue.Create(displayValue)
        };
    }

    private static JsonNode? CreateEmptyNode(ConfigurationNodeDefinition schema)
    {
        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Object or ConfigurationNodeKind.Dictionary => new JsonObject(),
            ConfigurationNodeKind.List => new JsonArray(),
            _ => null
        };
    }

    private static JsonNode? RedactForExport(
        JsonNode? node,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        out IReadOnlyList<string> redactedPaths)
    {
        var paths = new List<string>();
        var redacted = RedactNode(node, schema, path, paths);
        redactedPaths = paths;
        return redacted;
    }

    private static JsonNode? RedactNode(
        JsonNode? node,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        List<string> redactedPaths)
    {
        if (node is null)
        {
            return null;
        }

        if (schema.IsSensitive)
        {
            redactedPaths.Add(path.ToCanonicalString());
            return JsonValue.Create("***");
        }

        switch (node)
        {
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Object:
                foreach (var child in schema.Children)
                {
                    if (jsonObject.TryGetPropertyValue(child.Name, out var childValue))
                    {
                        var redacted = RedactNode(
                            childValue,
                            child,
                            path.Append(new PropertySegment(child.Name)),
                            redactedPaths);
                        if (!ReferenceEquals(childValue, redacted))
                        {
                            jsonObject[child.Name] = redacted;
                        }
                    }
                }
                break;
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Dictionary && schema.DictionaryTemplate is { } dictionary:
                foreach (var key in jsonObject.Select(static pair => pair.Key).ToArray())
                {
                    var childValue = jsonObject[key];
                    var redacted = RedactNode(
                        childValue,
                        dictionary.ValueTemplate,
                        path.Append(new DictionaryKeySegment(key)),
                        redactedPaths);
                    if (!ReferenceEquals(childValue, redacted))
                    {
                        jsonObject[key] = redacted;
                    }
                }
                break;
            case JsonArray jsonArray when schema.NodeKind == ConfigurationNodeKind.List && schema.ListTemplate is { } list:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    var itemPath = ResolveListItemPath(path, list, index, jsonArray[index]);
                    var childValue = jsonArray[index];
                    var redacted = RedactNode(childValue, list.ItemTemplate, itemPath, redactedPaths);
                    if (!ReferenceEquals(childValue, redacted))
                    {
                        jsonArray[index] = redacted;
                    }
                }
                break;
        }

        return node;
    }

    private static LogicalPath ResolveListItemPath(
        LogicalPath listPath,
        ConfigurationListTemplate listTemplate,
        int index,
        JsonNode? item)
    {
        if (listTemplate.SupportsPerItemMutation)
        {
            var key = ReadObjectScalar(item, listTemplate.ItemKeyPropertyName!);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return listPath.Append(new ListItemKeySegment(key));
            }
        }

        return listPath.Append(new ListIndexSegment(index));
    }

    private static Dictionary<string, JsonNode?> BuildListItemMap(JsonArray? array, ConfigurationListTemplate listTemplate)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        if (array is null)
        {
            return result;
        }

        for (var index = 0; index < array.Count; index++)
        {
            var item = array[index];
            var key = ReadObjectScalar(item, listTemplate.ItemKeyPropertyName!);
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = item;
            }
        }

        return result;
    }

    private static string? ReadObjectScalar(JsonNode? item, string propertyName)
    {
        if (item is not JsonObject itemObject || !itemObject.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(value.ToJsonString(COMPACT_JSON_OPTIONS));
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool JsonEquivalent(JsonNode? left, JsonNode? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            left.ToJsonString(COMPACT_JSON_OPTIONS),
            right.ToJsonString(COMPACT_JSON_OPTIONS),
            StringComparison.Ordinal);
    }

    private static string? DisplayNode(JsonNode? node, ConfigurationNodeDefinition schema)
    {
        if (node is null)
        {
            return null;
        }

        if (schema.IsSensitive)
        {
            return null;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Scalar)
        {
            using var document = JsonDocument.Parse(node.ToJsonString(COMPACT_JSON_OPTIONS));
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : document.RootElement.GetRawText();
        }

        return ToReadableJson(node);
    }

    private static string ToReadableJson(JsonNode? node)
    {
        return node?.ToJsonString(READABLE_JSON_OPTIONS) ?? "null";
    }

    private sealed class DraftAnalysisState(
        ConfigurationDefinition definition,
        IReadOnlyList<string> redactedPaths,
        bool compactChanges)
    {
        private readonly HashSet<string> _redactedPaths = redactedPaths.ToHashSet(StringComparer.Ordinal);

        public bool CompactChanges { get; } = compactChanges;

        public List<ConfigurationDraftChange> Changes { get; } = [];

        public List<ConfigurationDraftValidationIssue> ValidationIssues { get; } = [];

        public List<ConfigurationApiDiagnostic> Diagnostics { get; } = [];

        public int UnchangedCount { get; set; }

        public int RedactedSkipCount { get; set; }

        public bool IsRedacted(string canonicalPath)
        {
            if (!_redactedPaths.Contains(canonicalPath))
            {
                return false;
            }

            Diagnostics.Add(new ConfigurationApiDiagnostic
            {
                Severity = ConfigurationApiDiagnosticSeverity.Warning,
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = canonicalPath,
                Message = $"Redacted path '{canonicalPath}' was skipped."
            });
            return true;
        }
    }
}
