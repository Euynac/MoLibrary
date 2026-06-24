using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.Api;

internal sealed partial class ConfigurationApiService
{
    public async Task<ConfigurationJsonEditorDocumentResponse> BuildJsonEditorDocumentAsync(
        string definitionKey,
        string? scopePath)
    {
        var definition = await LoadDefinitionAsync(definitionKey);
        var path = ParsePath(scopePath);
        var scopeNode = ResolveNode(definition, path);
        var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, path);
        var node = ParseEffectiveNode(scopeNode, effectiveValue.DisplayValue);
        var redacted = RedactForExport(node?.DeepClone(), scopeNode, path, out var redactedPaths);

        return new ConfigurationJsonEditorDocumentResponse
        {
            DefinitionKey = definition.DefinitionKey,
            ScopePath = path.ToCanonicalString(),
            Json = ToReadableJson(redacted),
            RedactedPaths = redactedPaths,
            SchemaVersion = definition.SchemaVersion,
            ValueVersion = effectiveValue.Version
        };
    }

    public async Task<ConfigurationJsonDraftAnalyzeResult> AnalyzeJsonDraftAsync(ConfigurationJsonDraftAnalyzeRequest request)
    {
        var definition = await LoadDefinitionAsync(request.DefinitionKey);
        var scopePath = ParsePath(request.ScopePath);
        var scopeNode = ResolveNode(definition, scopePath);
        var currentEffective = await LoadEffectiveValueAsync(definition.DefinitionKey, scopePath);
        var current = ParseEffectiveNode(scopeNode, currentEffective.DisplayValue);

        JsonNode? incoming;
        try
        {
            incoming = JsonNode.Parse(string.IsNullOrWhiteSpace(request.Json) ? "null" : request.Json);
        }
        catch (JsonException ex)
        {
            return new ConfigurationJsonDraftAnalyzeResult
            {
                DefinitionKey = definition.DefinitionKey,
                DefinitionDisplayName = definition.DisplayName,
                ScopePath = scopePath.ToCanonicalString(),
                IsJsonValid = false,
                ParseError = ex.Message,
                Diagnostics =
                [
                    Error(definition.DefinitionKey, scopePath, $"Invalid JSON: {ex.Message}")
                ]
            };
        }

        var state = new DraftAnalysisState(definition, request.RedactedPaths, request.CompactChanges);
        await CollectChangesAsync(definition, scopeNode, scopePath, current, incoming, currentEffective, state);

        return new ConfigurationJsonDraftAnalyzeResult
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            ScopePath = scopePath.ToCanonicalString(),
            Changes = state.Changes,
            ValidationIssues = state.ValidationIssues,
            Diagnostics = state.Diagnostics,
            UnchangedCount = state.UnchangedCount,
            RedactedSkipCount = state.RedactedSkipCount
        };
    }

    private async Task CollectChangesAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? current,
        JsonNode? incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        var canonicalPath = path.ToCanonicalString();
        if (state.IsRedacted(canonicalPath))
        {
            state.RedactedSkipCount++;
            return;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Scalar)
        {
            await AddLeafChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
            return;
        }

        switch (schema.NodeKind)
        {
            case ConfigurationNodeKind.Object when incoming is JsonObject incomingObject:
                await CollectObjectChangesAsync(definition, schema, path, current as JsonObject, incomingObject, scopeEffectiveValue, state);
                break;
            case ConfigurationNodeKind.Dictionary when incoming is JsonObject incomingDictionary:
                await CollectDictionaryChangesAsync(definition, schema, path, current as JsonObject, incomingDictionary, scopeEffectiveValue, state);
                break;
            case ConfigurationNodeKind.List when incoming is JsonArray incomingList:
                await CollectListChangesAsync(definition, schema, path, current as JsonArray, incomingList, scopeEffectiveValue, state);
                break;
            default:
                await AddContainerChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
                break;
        }
    }

    private async Task CollectObjectChangesAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonObject? current,
        JsonObject incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        var knownChildren = schema.Children.ToDictionary(static child => child.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var property in incoming)
        {
            if (!knownChildren.ContainsKey(property.Key))
            {
                state.Diagnostics.Add(new ConfigurationApiDiagnostic
                {
                    Severity = ConfigurationApiDiagnosticSeverity.Warning,
                    DefinitionKey = definition.DefinitionKey,
                    LogicalPath = path.Append(new PropertySegment(property.Key)).ToCanonicalString(),
                    Message = $"Unknown configuration path '{path.Append(new PropertySegment(property.Key)).ToCanonicalString()}' was ignored."
                });
            }
        }

        foreach (var child in schema.Children)
        {
            var childPath = path.Append(new PropertySegment(child.Name));
            JsonNode? currentChild = null;
            current?.TryGetPropertyValue(child.Name, out currentChild);
            incoming.TryGetPropertyValue(child.Name, out var incomingChild);

            if (incomingChild is null)
            {
                if (currentChild is not null)
                {
                    await AddRemoveChangeAsync(definition, child, childPath, currentChild, scopeEffectiveValue, state);
                }

                continue;
            }

            await CollectChangesAsync(definition, child, childPath, currentChild, incomingChild, scopeEffectiveValue, state);
        }
    }

    private async Task CollectDictionaryChangesAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonObject? current,
        JsonObject incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        if (schema.DictionaryTemplate is not { } dictionaryTemplate)
        {
            await AddContainerChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
            return;
        }

        var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (current is not null)
        {
            foreach (var key in current.Select(static pair => pair.Key))
            {
                keys.Add(key);
            }
        }

        foreach (var key in incoming.Select(static pair => pair.Key))
        {
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            JsonNode? currentChild = null;
            current?.TryGetPropertyValue(key, out currentChild);
            incoming.TryGetPropertyValue(key, out var incomingChild);
            var childPath = path.Append(new DictionaryKeySegment(key));
            if (incomingChild is null)
            {
                await AddRemoveChangeAsync(definition, dictionaryTemplate.ValueTemplate, childPath, currentChild, scopeEffectiveValue, state);
                continue;
            }

            if (currentChild is null && state.CompactChanges)
            {
                await AddContainerChangeAsync(definition, dictionaryTemplate.ValueTemplate, childPath, null, incomingChild, scopeEffectiveValue, state);
                continue;
            }

            await CollectChangesAsync(
                definition,
                dictionaryTemplate.ValueTemplate,
                childPath,
                currentChild,
                incomingChild,
                scopeEffectiveValue,
                state);
        }
    }

    private async Task CollectListChangesAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonArray? current,
        JsonArray incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        if (schema.ListTemplate is not { SupportsPerItemMutation: true } listTemplate)
        {
            await AddContainerChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
            return;
        }

        var currentItems = BuildListItemMap(current, listTemplate);
        var incomingItems = BuildListItemMap(incoming, listTemplate);
        var keys = new SortedSet<string>(currentItems.Keys, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(incomingItems.Keys);

        foreach (var key in keys)
        {
            currentItems.TryGetValue(key, out var currentChild);
            incomingItems.TryGetValue(key, out var incomingChild);
            var childPath = path.Append(new ListItemKeySegment(key));
            if (incomingChild is null)
            {
                await AddRemoveChangeAsync(definition, listTemplate.ItemTemplate, childPath, currentChild, scopeEffectiveValue, state);
                continue;
            }

            if (currentChild is null && state.CompactChanges)
            {
                await AddContainerChangeAsync(definition, listTemplate.ItemTemplate, childPath, null, incomingChild, scopeEffectiveValue, state);
                continue;
            }

            await CollectChangesAsync(
                definition,
                listTemplate.ItemTemplate,
                childPath,
                currentChild,
                incomingChild,
                scopeEffectiveValue,
                state);
        }
    }

    private async Task AddLeafChangeAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? current,
        JsonNode? incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        if (JsonEquivalent(current, incoming))
        {
            state.UnchangedCount++;
            return;
        }

        await AddSetChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
    }

    private async Task AddContainerChangeAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? current,
        JsonNode? incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        if (JsonEquivalent(current, incoming))
        {
            state.UnchangedCount++;
            return;
        }

        await AddSetChangeAsync(definition, schema, path, current, incoming, scopeEffectiveValue, state);
    }

    private async Task AddSetChangeAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? current,
        JsonNode? incoming,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        var value = incoming?.DeepClone();
        var request = new ConfigurationMutationRequest
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = path,
            MutationKind = ConfigurationMutationKind.Set,
            Value = ConfigurationStoredValue.FromJson(value?.ToJsonString(COMPACT_JSON_OPTIONS) ?? "null"),
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = scopeEffectiveValue.Version
        };

        if (!ValidateDraftMutation(definition, schema, path, request, value, state))
        {
            return;
        }

        var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, path);
        var source = effectiveValue.EffectiveSource;
        var targetsExternalSource = schema.NodeKind == ConfigurationNodeKind.Scalar
                                    && source is { Kind: not ConfigurationSourceKind.MonicaEffectiveStore, IsWritable: true };

        state.Changes.Add(new ConfigurationDraftChange
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            LogicalPath = path.ToCanonicalString(),
            NodeDisplayName = DisplayName(schema),
            DisplayChangeKind = current is null ? ConfigurationDraftChangeKind.Added : ConfigurationDraftChangeKind.Modified,
            MutationKind = ConfigurationMutationKind.Set,
            TargetKind = targetsExternalSource
                ? ConfigurationMutationTargetKind.ExternalConfigurationSource
                : ConfigurationMutationTargetKind.MonicaEffectiveStore,
            SourceKey = targetsExternalSource ? source?.SourceKey : null,
            SourceDisplayName = targetsExternalSource ? source?.DisplayName : null,
            Value = value,
            OriginalDisplayValue = DisplayNode(current, schema),
            NewDisplayValue = DisplayNode(incoming, schema),
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = targetsExternalSource ? null : effectiveValue.Version ?? scopeEffectiveValue.Version,
            ExpectedSourceRevision = targetsExternalSource && source is not null
                ? await LoadSourceRevisionOrNullAsync(source.SourceKey)
                : null,
            IsSensitive = schema.IsSensitive,
            NodeKind = schema.NodeKind,
            ValueKind = schema.ValueKind,
            ReloadBehavior = ResolveReloadBehavior(definition, schema)
        });
    }

    private async Task AddRemoveChangeAsync(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonNode? current,
        ConfigurationEffectiveValue scopeEffectiveValue,
        DraftAnalysisState state)
    {
        if (current is null)
        {
            state.UnchangedCount++;
            return;
        }

        var request = new ConfigurationMutationRequest
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = path,
            MutationKind = ConfigurationMutationKind.Remove,
            Value = ConfigurationStoredValue.Null,
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = scopeEffectiveValue.Version
        };

        if (!ValidateDraftMutation(definition, schema, path, request, null, state))
        {
            return;
        }

        var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, path);
        var source = effectiveValue.EffectiveSource;
        var targetsExternalSource = schema.NodeKind == ConfigurationNodeKind.Scalar
                                    && source is { Kind: not ConfigurationSourceKind.MonicaEffectiveStore, IsWritable: true };

        state.Changes.Add(new ConfigurationDraftChange
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            LogicalPath = path.ToCanonicalString(),
            NodeDisplayName = DisplayName(schema),
            DisplayChangeKind = ConfigurationDraftChangeKind.Removed,
            MutationKind = ConfigurationMutationKind.Remove,
            TargetKind = targetsExternalSource
                ? ConfigurationMutationTargetKind.ExternalConfigurationSource
                : ConfigurationMutationTargetKind.MonicaEffectiveStore,
            SourceKey = targetsExternalSource ? source?.SourceKey : null,
            SourceDisplayName = targetsExternalSource ? source?.DisplayName : null,
            OriginalDisplayValue = DisplayNode(current, schema),
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = targetsExternalSource ? null : effectiveValue.Version ?? scopeEffectiveValue.Version,
            ExpectedSourceRevision = targetsExternalSource && source is not null
                ? await LoadSourceRevisionOrNullAsync(source.SourceKey)
                : null,
            IsSensitive = schema.IsSensitive,
            NodeKind = schema.NodeKind,
            ValueKind = schema.ValueKind,
            ReloadBehavior = ResolveReloadBehavior(definition, schema)
        });
    }

    private bool ValidateDraftMutation(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        ConfigurationMutationRequest request,
        JsonNode? invalidValue,
        DraftAnalysisState state)
    {
        try
        {
            validationCoordinator.Validate(definition, request);
            return true;
        }
        catch (Exception ex)
        {
            state.ValidationIssues.Add(new ConfigurationDraftValidationIssue
            {
                DefinitionKey = definition.DefinitionKey,
                DefinitionDisplayName = definition.DisplayName,
                LogicalPath = path.ToCanonicalString(),
                NodeDisplayName = DisplayName(schema),
                InvalidDisplayValue = DisplayNode(invalidValue, schema),
                ValidationError = ex.Message,
                IsSensitive = schema.IsSensitive,
                ValidationRules = schema.ValidationRules
            });
            return false;
        }
    }
}
