using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Collapses detailed UI edits under complex configuration scopes into readable container mutations.
/// </summary>
internal sealed class ConfigurationPendingChangeCompactor(IStringLocalizer<ConfigurationUIResource> localizer)
{
    /// <summary>
    /// Compacts detailed pending changes into the largest target-safe container writes for one edited scope.
    /// </summary>
    /// <param name="definition">The configuration definition being edited.</param>
    /// <param name="scopeNode">The edited schema scope.</param>
    /// <param name="effectiveValue">The current effective value for the edited scope.</param>
    /// <param name="changes">Detailed pending changes produced by the editor or import parser.</param>
    /// <param name="scalarEffectiveValues">Known scalar effective values used to verify source ownership.</param>
    /// <returns>Compacted changes when safe; otherwise the original detailed changes.</returns>
    public IReadOnlyList<PendingChange> Compact(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition scopeNode,
        ConfigurationEffectiveValue effectiveValue,
        IReadOnlyList<PendingChange> changes,
        IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue>? scalarEffectiveValues = null)
    {
        var scopedChanges = changes
            .Where(change => ConfigurationPendingValueDocumentBuilder.IsInScope(
                change,
                definition.DefinitionKey,
                scopeNode.RelativePath))
            .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
            .ToArray();

        if (scopeNode.NodeKind == ConfigurationNodeKind.Scalar || scopedChanges.Length == 0)
        {
            return scopedChanges;
        }

        var compacted = CompactScopes(definition, scopeNode, effectiveValue, scopedChanges, scalarEffectiveValues ?? EmptyScalarValues());
        return compacted.Count == 0
            ? []
            : compacted
                .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
                .ToArray();
    }

    private IReadOnlyList<PendingChange> CompactScopes(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition editScope,
        ConfigurationEffectiveValue effectiveValue,
        IReadOnlyList<PendingChange> scopedChanges,
        IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue> scalarEffectiveValues)
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var compacted = new List<PendingChange>();

        foreach (var candidate in BuildCandidates(definition, editScope, scopedChanges))
        {
            var candidateChanges = scopedChanges
                .Where(change => !covered.Contains(ChangeKey(change))
                                 && ConfigurationPendingValueDocumentBuilder.IsInScope(
                                     change,
                                     definition.DefinitionKey,
                                     candidate.Path))
                .ToArray();
            if (candidateChanges.Length == 0)
            {
                continue;
            }

            var signature = PendingChangeTargetSignature.TryCreate(candidateChanges);
            if (signature is null || !TargetOwnsScope(signature, candidate.Path, scalarEffectiveValues))
            {
                continue;
            }

            if (candidate.Schema is not { } candidateSchema)
            {
                continue;
            }

            var compactedChange = BuildCompactedChange(
                definition,
                editScope,
                candidateSchema,
                effectiveValue,
                candidate.Path,
                candidateChanges);
            if (compactedChange is null)
            {
                foreach (var change in candidateChanges)
                {
                    covered.Add(ChangeKey(change));
                }

                continue;
            }

            compacted.Add(compactedChange);
            foreach (var change in candidateChanges)
            {
                covered.Add(ChangeKey(change));
            }
        }

        compacted.AddRange(scopedChanges.Where(change => !covered.Contains(ChangeKey(change))));
        return compacted;
    }

    private PendingChange? BuildCompactedChange(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition editScope,
        ConfigurationNodeDefinition scopeSchema,
        ConfigurationEffectiveValue effectiveValue,
        LogicalPath scopePath,
        IReadOnlyList<PendingChange> changes)
    {
        if (changes is [var onlyChange]
            && onlyChange.LogicalPath.Equals(scopePath)
            && onlyChange.MutationKind == ConfigurationMutationKind.Remove)
        {
            return onlyChange;
        }

        var original = ReadOriginalScopeJson(editScope, scopeSchema, effectiveValue.DisplayValue, scopePath);
        var updated = ConfigurationPendingValueDocumentBuilder.ApplyPendingChanges(
            scopeSchema,
            definition.DefinitionKey,
            scopePath,
            CloneNode(original) ?? ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(scopeSchema),
            changes);

        if (JsonNode.DeepEquals(original, updated))
        {
            return null;
        }

        var representative = changes[0];
        var storedValue = ConfigurationStoredValue.FromJson(updated.ToJsonString());
        return representative with
        {
            LogicalPath = scopePath,
            NodeDisplayName = DisplayName(definition, scopeSchema, scopePath),
            MutationKind = ConfigurationMutationKind.Set,
            NewValue = storedValue,
            OriginalValue = ConfigurationStoredValue.FromJson(original.ToJsonString()),
            OriginalDisplayValue = DisplayJson(original, scopeSchema),
            NewDisplayValue = DisplayJson(updated, scopeSchema),
            IsSensitive = scopeSchema.IsSensitive,
            NodeKind = scopeSchema.NodeKind,
            ValueKind = scopeSchema.ValueKind,
            ReloadBehavior = EffectiveReloadBehaviorFor(definition, scopeSchema),
            ExpectedValueVersion = representative.TargetKind == ConfigurationMutationTargetKind.MonicaEffectiveStore
                ? effectiveValue.Version
                : null,
            SourceConfigurationPath = representative.TargetKind == ConfigurationMutationTargetKind.ExternalConfigurationSource
                ? ProjectPath(definition, scopePath)
                : representative.SourceConfigurationPath
        };
    }

    private static JsonNode ReadOriginalScopeJson(
        ConfigurationNodeDefinition editScope,
        ConfigurationNodeDefinition scopeSchema,
        string? effectiveJson,
        LogicalPath scopePath)
    {
        var root = ParseEffectiveJson(editScope, effectiveJson);
        if (scopePath.Equals(editScope.RelativePath))
        {
            return root;
        }

        var current = root;
        var currentSchema = editScope;
        foreach (var segment in scopePath.Segments.Skip(editScope.RelativePath.Depth))
        {
            current = GetExistingChild(current, currentSchema, segment);
            if (current is null)
            {
                return ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(scopeSchema);
            }

            currentSchema = ResolveChildSchema(currentSchema, segment) ?? scopeSchema;
        }

        return CloneNode(current) ?? ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(scopeSchema);
    }

    private static JsonNode ParseEffectiveJson(ConfigurationNodeDefinition editScope, string? effectiveJson)
    {
        if (string.IsNullOrWhiteSpace(effectiveJson))
        {
            return ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(editScope);
        }

        try
        {
            return JsonNode.Parse(effectiveJson) ?? ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(editScope);
        }
        catch (JsonException)
        {
            return editScope.RelativePath.Depth == 0
                ? ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(editScope)
                : JsonValue.Create(effectiveJson);
        }
    }

    private static IReadOnlyList<CompactionCandidate> BuildCandidates(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition editScope,
        IReadOnlyList<PendingChange> changes)
    {
        return changes
            .SelectMany(change => EnumerateCandidatePaths(editScope.RelativePath, change.LogicalPath))
            .DistinctBy(path => path.ToCanonicalString(), StringComparer.Ordinal)
            .Select(path => new CompactionCandidate(path, ResolveNodeByPath(definition, path)))
            .Where(candidate => candidate.Schema is { NodeKind: not ConfigurationNodeKind.Scalar })
            .OrderBy(candidate => candidate.Path.Depth)
            .ThenBy(candidate => candidate.Path.ToCanonicalString(), StringComparer.Ordinal)
            .ToArray()!;
    }

    private static IEnumerable<LogicalPath> EnumerateCandidatePaths(LogicalPath scopePath, LogicalPath changePath)
    {
        for (var depth = scopePath.Depth; depth <= changePath.Depth; depth++)
        {
            yield return new LogicalPath(changePath.Segments.Take(depth).ToArray());
        }
    }

    private static ConfigurationNodeDefinition? ResolveNodeByPath(ConfigurationDefinition definition, LogicalPath path)
    {
        var current = definition.Root;
        foreach (var segment in path.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static JsonNode? GetExistingChild(
        JsonNode? current,
        ConfigurationNodeDefinition currentSchema,
        ConfigurationPathSegment segment)
    {
        return current switch
        {
            JsonObject jsonObject when segment is PropertySegment or DictionaryKeySegment => jsonObject[segment.Value],
            JsonArray jsonArray when segment is ListIndexSegment listIndex && listIndex.Index >= 0 && listIndex.Index < jsonArray.Count => jsonArray[listIndex.Index],
            JsonArray jsonArray when segment is ListItemKeySegment itemKey => FindArrayItemByKey(jsonArray, currentSchema, itemKey.ItemKey),
            _ => null
        };
    }

    private static ConfigurationNodeDefinition? ResolveChildSchema(
        ConfigurationNodeDefinition current,
        ConfigurationPathSegment segment)
    {
        return segment switch
        {
            PropertySegment property => current.Children.FirstOrDefault(child =>
                string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
            DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
            ListIndexSegment or ListItemKeySegment => current.ListTemplate?.ItemTemplate,
            _ => null
        };
    }

    private static JsonNode? FindArrayItemByKey(
        JsonArray jsonArray,
        ConfigurationNodeDefinition listSchema,
        string itemKey)
    {
        var keyPropertyName = listSchema.ListTemplate?.ItemKeyPropertyName;
        if (string.IsNullOrWhiteSpace(keyPropertyName))
        {
            return null;
        }

        foreach (var item in jsonArray)
        {
            if (string.Equals(ReadObjectPropertyText(item, keyPropertyName), itemKey, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static string? ReadObjectPropertyText(JsonNode? node, string propertyName)
    {
        if (node is not JsonObject jsonObject || jsonObject[propertyName] is not { } value)
        {
            return null;
        }

        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TargetOwnsScope(
        PendingChangeTargetSignature signature,
        LogicalPath scopePath,
        IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue> scalarEffectiveValues)
    {
        var knownValues = scalarEffectiveValues
            .Where(pair => IsPrefix(scopePath, pair.Key))
            .ToArray();
        return knownValues.Length == 0
               || knownValues.All(pair => signature.Matches(pair.Value.EffectiveSource));
    }

    private static bool IsPrefix(LogicalPath ancestor, LogicalPath path)
    {
        if (ancestor.Depth > path.Depth)
        {
            return false;
        }

        for (var index = 0; index < ancestor.Depth; index++)
        {
            if (!ancestor.Segments[index].Equals(path.Segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private string DisplayName(ConfigurationDefinition definition, ConfigurationNodeDefinition schema, LogicalPath path)
    {
        if (path.Depth == 0)
        {
            return definition.DisplayName;
        }

        if (schema.RelativePath.Equals(path) && !string.IsNullOrWhiteSpace(schema.DisplayName))
        {
            return schema.DisplayName!;
        }

        return path.ToCanonicalString();
    }

    private string DisplayJson(JsonNode? node, ConfigurationNodeDefinition schema)
    {
        return ConfigurationJsonDisplayFormatter.Format(node, schema, localizer["State:Value:Sensitive"]);
    }

    private static ConfigurationReloadBehavior EffectiveReloadBehaviorFor(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema)
    {
        return schema.ResolveEffectiveReloadBehavior(definition);
    }

    private static string ProjectPath(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.SectionPath))
        {
            parts.Add(definition.SectionPath);
        }

        parts.AddRange(logicalPath.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString());
    }

    private static IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue> EmptyScalarValues()
    {
        return new Dictionary<LogicalPath, ConfigurationEffectiveValue>();
    }

    private static string ChangeKey(PendingChange change)
    {
        return $"{change.DefinitionKey}|{change.LogicalPath.ToCanonicalString()}|{change.TargetKind}|{change.SourceKey}";
    }

    private sealed record CompactionCandidate(LogicalPath Path, ConfigurationNodeDefinition? Schema);

    private sealed record PendingChangeTargetSignature(
        ConfigurationMutationTargetKind TargetKind,
        string? SourceKey,
        string? SourcePhysicalPath)
    {
        public static PendingChangeTargetSignature? TryCreate(IReadOnlyList<PendingChange> changes)
        {
            if (changes.Count == 0)
            {
                return null;
            }

            var signature = From(changes[0]);
            return changes.All(change => signature.Equals(From(change))) ? signature : null;
        }

        public bool Matches(ConfigurationSourceDescriptor? source)
        {
            if (source is not { Kind: not ConfigurationSourceKind.MonicaEffectiveStore })
            {
                return TargetKind == ConfigurationMutationTargetKind.MonicaEffectiveStore;
            }

            return TargetKind == ConfigurationMutationTargetKind.ExternalConfigurationSource
                   && (string.IsNullOrWhiteSpace(SourceKey)
                       || string.Equals(SourceKey, source.SourceKey, StringComparison.Ordinal))
                   && (string.IsNullOrWhiteSpace(SourcePhysicalPath)
                       || string.IsNullOrWhiteSpace(source.PhysicalPath)
                       || string.Equals(
                           Path.GetFullPath(SourcePhysicalPath),
                           Path.GetFullPath(source.PhysicalPath),
                           StringComparison.OrdinalIgnoreCase));
        }

        private static PendingChangeTargetSignature From(PendingChange change)
        {
            return new PendingChangeTargetSignature(
                change.TargetKind,
                change.SourceKey,
                change.SourcePhysicalPath);
        }
    }
}
