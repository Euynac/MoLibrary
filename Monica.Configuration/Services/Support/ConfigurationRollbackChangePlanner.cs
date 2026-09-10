using System.Text.Json;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Produces the smallest safely addressable configuration mutations for one effective-value rollback.
/// </summary>
internal static class ConfigurationRollbackChangePlanner
{
    public static IReadOnlyList<ConfigurationRollbackLeafValue> EnumerateLeaves(
        ConfigurationNodeDefinition rootSchema,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        var leaves = new List<ConfigurationRollbackLeafValue>();
        EnumerateLeaves(rootSchema, LogicalPath.Root, document.RootElement, leaves);
        return leaves;
    }

    public static IReadOnlyList<ConfigurationRollbackValueChange> Plan(
        ConfigurationNodeDefinition rootSchema,
        string currentJson,
        string targetJson)
    {
        using var currentDocument = JsonDocument.Parse(currentJson);
        using var targetDocument = JsonDocument.Parse(targetJson);
        var changes = new List<ConfigurationRollbackValueChange>();
        PlanNode(
            rootSchema,
            LogicalPath.Root,
            currentDocument.RootElement,
            currentExists: true,
            targetDocument.RootElement,
            targetExists: true,
            changes);
        return changes;
    }

    private static void PlanNode(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement current,
        bool currentExists,
        JsonElement target,
        bool targetExists,
        ICollection<ConfigurationRollbackValueChange> changes)
    {
        if (currentExists && targetExists && ConfigurationJsonSemanticComparer.Equals(current, target))
        {
            return;
        }

        // Sensitive containers are one mutation boundary. Descending would expose secret dictionary keys,
        // list shape, or child paths through the rollback plan even when scalar values are redacted.
        if (schema.IsSensitive)
        {
            AddChange(schema, path, current, currentExists, target, targetExists, changes);
            return;
        }

        if (!currentExists || !targetExists)
        {
            AddChange(schema, path, current, currentExists, target, targetExists, changes);
            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Object
            && current.ValueKind == JsonValueKind.Object
            && target.ValueKind == JsonValueKind.Object)
        {
            // Only schema-declared children can be written back. When the walk finds no child change,
            // any remaining difference lives in properties the current schema no longer declares and
            // must not become a whole-object replacement that persists unknown data.
            foreach (var child in schema.Children)
            {
                var hasCurrent = TryGetProperty(current, child.Name, out var currentChild);
                var hasTarget = TryGetProperty(target, child.Name, out var targetChild);
                if (!hasCurrent && !hasTarget)
                {
                    continue;
                }

                PlanNode(
                    child,
                    path.Append(new PropertySegment(child.Name)),
                    currentChild,
                    hasCurrent,
                    targetChild,
                    hasTarget,
                    changes);
            }

            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Dictionary
            && schema.DictionaryTemplate is { } dictionary
            && current.ValueKind == JsonValueKind.Object
            && target.ValueKind == JsonValueKind.Object)
        {
            var keys = current.EnumerateObject().Select(static property => property.Name)
                .Concat(target.EnumerateObject().Select(static property => property.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                var hasCurrent = TryGetProperty(current, key, out var currentValue);
                var hasTarget = TryGetProperty(target, key, out var targetValue);
                PlanNode(
                    dictionary.ValueTemplate,
                    path.Append(new DictionaryKeySegment(key)),
                    currentValue,
                    hasCurrent,
                    targetValue,
                    hasTarget,
                    changes);
            }

            return;
        }

        // A list is intentionally one rollback boundary. This preserves ordering and avoids translating stable
        // item identities into source-specific numeric indexes when the effective values span multiple providers.
        AddChange(schema, path, current, currentExists, target, targetExists, changes);
    }

    private static void AddChange(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement current,
        bool currentExists,
        JsonElement target,
        bool targetExists,
        ICollection<ConfigurationRollbackValueChange> changes)
    {
        changes.Add(new ConfigurationRollbackValueChange(
            path,
            schema,
            targetExists ? ConfigurationMutationKind.Set : ConfigurationMutationKind.Remove,
            currentExists ? current.GetRawText() : null,
            targetExists ? target.GetRawText() : null));
    }

    private static void EnumerateLeaves(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ICollection<ConfigurationRollbackLeafValue> leaves)
    {
        if (schema.IsSensitive)
        {
            leaves.Add(new ConfigurationRollbackLeafValue(path, schema, value.GetRawText()));
            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Object && value.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in schema.Children)
            {
                if (TryGetProperty(value, child.Name, out var childValue))
                {
                    EnumerateLeaves(child, path.Append(new PropertySegment(child.Name)), childValue, leaves);
                }
            }

            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Dictionary
            && schema.DictionaryTemplate is { } dictionary
            && value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                EnumerateLeaves(
                    dictionary.ValueTemplate,
                    path.Append(new DictionaryKeySegment(property.Name)),
                    property.Value,
                    leaves);
            }

            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.List
            && schema.ListTemplate is { } list
            && value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                EnumerateLeaves(
                    list.ItemTemplate,
                    path.Append(new ListIndexSegment(index++)),
                    item,
                    leaves);
            }

            return;
        }

        leaves.Add(new ConfigurationRollbackLeafValue(path, schema, value.GetRawText()));
    }

    private static bool TryGetProperty(JsonElement value, string propertyName, out JsonElement propertyValue)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }
        }

        propertyValue = default;
        return false;
    }
}

internal sealed record ConfigurationRollbackValueChange(
    LogicalPath LogicalPath,
    ConfigurationNodeDefinition Schema,
    ConfigurationMutationKind MutationKind,
    string? CurrentJson,
    string? TargetJson);

internal sealed record ConfigurationRollbackLeafValue(
    LogicalPath LogicalPath,
    ConfigurationNodeDefinition Schema,
    string Json);
