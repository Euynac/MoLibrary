using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Converts operator-provided JSON snapshots into staged configuration changes and validation issues.
/// </summary>
internal sealed class ConfigurationJsonDraftService(
    IStringLocalizer<ConfigurationUIResource> localizer,
    ConfigurationPendingChangeCompactor changeCompactor)
{
    /// <summary>
    /// Analyzes one JSON snapshot against a definition scope.
    /// </summary>
    /// <param name="request">The draft request.</param>
    /// <returns>The draft result.</returns>
    public ConfigurationJsonDraftResult Analyze(ConfigurationJsonDraftRequest request)
    {
        var state = new DraftState(request, localizer, changeCompactor);
        return state.Analyze();
    }

    /// <summary>
    /// Builds the operator-facing JSON value for an editable scope, including existing staged changes.
    /// </summary>
    /// <param name="definition">The owning definition.</param>
    /// <param name="scopeNode">The scope node.</param>
    /// <param name="effectiveValue">The current effective value.</param>
    /// <param name="pendingChanges">Existing pending changes.</param>
    /// <returns>Redacted JSON text plus redacted canonical paths.</returns>
    public ConfigurationJsonEditorDocument BuildEditorDocument(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition scopeNode,
        ConfigurationEffectiveValue effectiveValue,
        IReadOnlyList<PendingChange> pendingChanges)
    {
        var node = ConfigurationPendingValueDocumentBuilder.Build(
            scopeNode,
            definition.DefinitionKey,
            effectiveValue.DisplayValue,
            pendingChanges);
        var redacted = RedactForExport(node, scopeNode, out var redactedPaths);
        var json = redacted?.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions) ?? "null";
        return new ConfigurationJsonEditorDocument(json, redactedPaths);
    }

    /// <summary>
    /// Redacts sensitive values in a concrete JSON document and returns the redacted paths.
    /// </summary>
    /// <param name="node">The concrete JSON document.</param>
    /// <param name="schema">The schema for the document.</param>
    /// <param name="redactedPaths">Canonical paths redacted in the returned document.</param>
    /// <returns>A redacted clone of <paramref name="node"/>.</returns>
    public JsonNode? RedactForExport(
        JsonNode? node,
        ConfigurationNodeDefinition schema,
        out IReadOnlyList<string> redactedPaths)
    {
        var paths = new List<string>();
        var clone = CloneNode(node);
        var redacted = RedactConcreteNode(clone, schema, schema.RelativePath, paths);
        redactedPaths = paths;
        return redacted;
    }

    /// <summary>
    /// Collects sensitive logical paths under a schema node.
    /// </summary>
    /// <param name="scopeNode">The schema scope.</param>
    /// <returns>Canonical sensitive logical paths.</returns>
    public IReadOnlyList<LogicalPath> CollectSensitivePaths(ConfigurationNodeDefinition scopeNode)
    {
        return EnumerateSensitivePaths(scopeNode).ToArray();
    }

    private static IEnumerable<LogicalPath> EnumerateSensitivePaths(ConfigurationNodeDefinition node)
    {
        if (node.IsSensitive)
        {
            yield return node.RelativePath;
        }

        foreach (var child in node.Children)
        {
            foreach (var path in EnumerateSensitivePaths(child))
            {
                yield return path;
            }
        }

        if (node.DictionaryTemplate is { } dictionaryTemplate)
        {
            foreach (var path in EnumerateSensitiveTemplatePaths(dictionaryTemplate.ValueTemplate, node.RelativePath))
            {
                yield return path;
            }
        }

        if (node.ListTemplate is { } listTemplate)
        {
            foreach (var path in EnumerateSensitiveTemplatePaths(listTemplate.ItemTemplate, node.RelativePath))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<LogicalPath> EnumerateSensitiveTemplatePaths(ConfigurationNodeDefinition template, LogicalPath ownerPath)
    {
        if (template.IsSensitive)
        {
            yield return ownerPath;
        }

        foreach (var child in template.Children)
        {
            foreach (var path in EnumerateSensitiveTemplatePaths(child, ownerPath.Append(new PropertySegment(child.Name))))
            {
                yield return path;
            }
        }
    }

    private JsonNode? RedactConcreteNode(
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
            return JsonValue.Create(localizer["ImportExport:RedactedValue"].Value);
        }

        switch (node)
        {
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Object:
                foreach (var child in schema.Children)
                {
                    var current = jsonObject[child.Name];
                    var redacted = RedactConcreteNode(
                        current,
                        child,
                        path.Append(new PropertySegment(child.Name)),
                        redactedPaths);
                    if (!ReferenceEquals(current, redacted))
                    {
                        jsonObject[child.Name] = redacted;
                    }
                }
                break;
            case JsonObject jsonObject when schema.NodeKind == ConfigurationNodeKind.Dictionary && schema.DictionaryTemplate is { } dictionaryTemplate:
                foreach (var pair in jsonObject.ToArray())
                {
                    var redacted = RedactConcreteNode(
                        pair.Value,
                        dictionaryTemplate.ValueTemplate,
                        path.Append(new DictionaryKeySegment(pair.Key)),
                        redactedPaths);
                    if (!ReferenceEquals(pair.Value, redacted))
                    {
                        jsonObject[pair.Key] = redacted;
                    }
                }
                break;
            case JsonArray jsonArray when schema.NodeKind == ConfigurationNodeKind.List && schema.ListTemplate is { } listTemplate:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    var current = jsonArray[index];
                    var itemPath = ResolveConcreteListItemPath(path, listTemplate, index, current);
                    var redacted = RedactConcreteNode(current, listTemplate.ItemTemplate, itemPath, redactedPaths);
                    if (!ReferenceEquals(current, redacted))
                    {
                        jsonArray[index] = redacted;
                    }
                }
                break;
        }

        return node;
    }

    private static LogicalPath ResolveConcreteListItemPath(
        LogicalPath listPath,
        ConfigurationListTemplate listTemplate,
        int index,
        JsonNode? item)
    {
        if (listTemplate.SupportsPerItemMutation)
        {
            var itemKey = ReadObjectScalar(item, listTemplate.ItemKeyPropertyName!);
            if (!string.IsNullOrWhiteSpace(itemKey))
            {
                return listPath.Append(new ListItemKeySegment(itemKey));
            }
        }

        return listPath.Append(new ListIndexSegment(index));
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString());
    }

    private static string? ReadObjectScalar(JsonNode? item, string propertyName)
    {
        if (item is not JsonObject itemObject)
        {
            return null;
        }

        var value = itemObject[propertyName];
        if (value is null)
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

    private sealed class DraftState(
        ConfigurationJsonDraftRequest request,
        IStringLocalizer<ConfigurationUIResource> localizer,
        ConfigurationPendingChangeCompactor changeCompactor)
    {
        private readonly List<PendingChange> _changes = [];
        private readonly List<ConfigurationValidationIssue> _issues = [];
        private readonly List<ConfigurationImportDiagnostic> _diagnostics = [];
        private readonly JsonNode? _originalNode = ParseOriginal(request.EffectiveValue.DisplayValue, request.ScopeNode);
        private int _unchangedCount;
        private int _redactedSkipCount;

        public ConfigurationJsonDraftResult Analyze()
        {
            var json = string.IsNullOrWhiteSpace(request.Json) ? "null" : request.Json;
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                return InvalidJsonResult(
                    ex.Message,
                    [
                        Diagnostic(ConfigurationImportDiagnosticSeverity.Error, request.ScopeNode.RelativePath,
                            localizer["ImportExport:Diagnostics:InvalidJson", ex.Message])
                    ]);
            }

            using (document)
            {
                var duplicateProperties = FindDuplicateProperties(document.RootElement);
                if (duplicateProperties.Count > 0)
                {
                    var diagnostics = duplicateProperties
                        .Select(duplicate => Diagnostic(
                            ConfigurationImportDiagnosticSeverity.Error,
                            duplicate.Path,
                            localizer["ImportExport:Diagnostics:DuplicateProperty", duplicate.Name, duplicate.Path.ToCanonicalString()]))
                        .ToArray();
                    return InvalidJsonResult(diagnostics[0].Message, diagnostics);
                }
            }

            JsonNode? incoming;
            try
            {
                incoming = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                return InvalidJsonResult(
                    ex.Message,
                    [
                        Diagnostic(ConfigurationImportDiagnosticSeverity.Error, request.ScopeNode.RelativePath,
                            localizer["ImportExport:Diagnostics:InvalidJson", ex.Message])
                    ]);
            }

            var normalizedOriginal = NormalizeJsonNode(request.ScopeNode, _originalNode);
            var normalizedIncoming = NormalizeJsonNode(request.ScopeNode, incoming);

            Visit(request.ScopeNode, request.ScopeNode.RelativePath, normalizedOriginal, normalizedIncoming, valueMissing: false);

            var outputChanges = request.CompactChanges
                ? changeCompactor.Compact(
                    request.Definition,
                    request.ScopeNode,
                    request.EffectiveValue,
                    _changes,
                    request.ScalarEffectiveValues)
                : _changes;

            return new ConfigurationJsonDraftResult
            {
                DefinitionKey = request.Definition.DefinitionKey,
                DefinitionDisplayName = request.Definition.DisplayName,
                ScopePath = request.ScopeNode.RelativePath,
                Changes = outputChanges
                    .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
                    .ToArray(),
                ValidationIssues = _issues
                    .OrderBy(issue => issue.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
                    .ToArray(),
                Diagnostics = _diagnostics.ToArray(),
                UnchangedCount = _unchangedCount,
                RedactedSkipCount = _redactedSkipCount
            };
        }

        private ConfigurationJsonDraftResult InvalidJsonResult(
            string message,
            IReadOnlyList<ConfigurationImportDiagnostic> diagnostics)
        {
            return new ConfigurationJsonDraftResult
            {
                DefinitionKey = request.Definition.DefinitionKey,
                DefinitionDisplayName = request.Definition.DisplayName,
                ScopePath = request.ScopeNode.RelativePath,
                IsJsonValid = false,
                ParseError = message,
                Diagnostics = diagnostics
            };
        }

        private IReadOnlyList<DuplicateJsonProperty> FindDuplicateProperties(JsonElement element)
        {
            var duplicates = new List<DuplicateJsonProperty>();
            FindDuplicateProperties(request.ScopeNode, request.ScopeNode.RelativePath, element, duplicates);
            return duplicates;
        }

        private void FindDuplicateProperties(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonElement element,
            List<DuplicateJsonProperty> duplicates)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    FindDuplicateObjectProperties(schema, path, element, duplicates);
                    break;
                case JsonValueKind.Array when schema.ListTemplate is { } listTemplate:
                    FindDuplicateListItemProperties(path, element, listTemplate, duplicates);
                    break;
            }
        }

        private void FindDuplicateObjectProperties(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonElement element,
            List<DuplicateJsonProperty> duplicates)
        {
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = ResolveJsonPropertyPath(schema, path, property.Name);
                if (!propertyNames.Add(property.Name))
                {
                    duplicates.Add(new DuplicateJsonProperty(property.Name, propertyPath));
                }

                if (ResolveJsonPropertySchema(schema, property.Name) is { } childSchema)
                {
                    FindDuplicateProperties(childSchema, propertyPath, property.Value, duplicates);
                }
            }
        }

        private void FindDuplicateListItemProperties(
            LogicalPath path,
            JsonElement element,
            ConfigurationListTemplate listTemplate,
            List<DuplicateJsonProperty> duplicates)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemPath = ResolveJsonListItemPath(path, listTemplate, index, item);
                FindDuplicateProperties(listTemplate.ItemTemplate, itemPath, item, duplicates);
                index++;
            }
        }

        private static ConfigurationNodeDefinition? ResolveJsonPropertySchema(
            ConfigurationNodeDefinition schema,
            string propertyName)
        {
            return schema.NodeKind switch
            {
                ConfigurationNodeKind.Object => schema.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, propertyName, StringComparison.Ordinal)),
                ConfigurationNodeKind.Dictionary => schema.DictionaryTemplate?.ValueTemplate,
                _ => null
            };
        }

        private static LogicalPath ResolveJsonPropertyPath(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            string propertyName)
        {
            return schema.NodeKind == ConfigurationNodeKind.Dictionary
                ? path.Append(new DictionaryKeySegment(propertyName))
                : path.Append(new PropertySegment(propertyName));
        }

        private static LogicalPath ResolveJsonListItemPath(
            LogicalPath listPath,
            ConfigurationListTemplate listTemplate,
            int index,
            JsonElement item)
        {
            if (listTemplate.SupportsPerItemMutation
                && TryReadObjectScalar(item, listTemplate.ItemKeyPropertyName!) is { Length: > 0 } itemKey)
            {
                return listPath.Append(new ListItemKeySegment(itemKey));
            }

            return listPath.Append(new ListIndexSegment(index));
        }

        private static string? TryReadObjectScalar(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in item.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                return property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }

            return null;
        }

        private void Visit(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming,
            bool valueMissing)
        {
            if (ShouldSkipRedacted(path))
            {
                _redactedSkipCount++;
                return;
            }

            if (valueMissing)
            {
                VisitMissing(schema, path, original);
                return;
            }

            switch (schema.NodeKind)
            {
                case ConfigurationNodeKind.Scalar:
                    VisitScalar(schema, path, original, incoming);
                    break;
                case ConfigurationNodeKind.Object:
                    VisitObject(schema, path, original, incoming);
                    break;
                case ConfigurationNodeKind.Dictionary:
                    VisitDictionary(schema, path, original, incoming);
                    break;
                case ConfigurationNodeKind.List:
                    VisitList(schema, path, original, incoming);
                    break;
            }
        }

        private void VisitMissing(ConfigurationNodeDefinition schema, LogicalPath path, JsonNode? original)
        {
            if (original is null)
            {
                _unchangedCount++;
                return;
            }

            if (schema.NodeKind == ConfigurationNodeKind.Scalar && schema.ValidationRules.OfType<RequiredRule>().Any())
            {
                AddIssue(schema, path, localizer["State:Editor:Required"], null);
                return;
            }

            StageRemove(schema, path, original);
        }

        private void VisitObject(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming)
        {
            if (incoming is not JsonObject incomingObject)
            {
                AddIssue(schema, path, localizer["ImportExport:Diagnostics:ExpectedObject"], DisplayJson(incoming, schema));
                return;
            }

            var originalObject = original as JsonObject;
            ReportUnknownProperties(schema, path, incomingObject);
            foreach (var child in schema.Children)
            {
                var childPath = path.Append(new PropertySegment(child.Name));
                Visit(child, childPath, originalObject?[child.Name], incomingObject[child.Name], !incomingObject.ContainsKey(child.Name));
            }
        }

        private void VisitDictionary(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming)
        {
            if (incoming is not JsonObject incomingObject)
            {
                AddIssue(schema, path, localizer["ImportExport:Diagnostics:ExpectedObject"], DisplayJson(incoming, schema));
                return;
            }

            var valueSchema = schema.DictionaryTemplate?.ValueTemplate;
            if (valueSchema is null)
            {
                AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, path, localizer["ImportExport:Diagnostics:UnsupportedSchema"]);
                return;
            }

            var originalObject = original as JsonObject;
            var keys = incomingObject.Select(pair => pair.Key)
                .Concat(originalObject?.Select(pair => pair.Key) ?? [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToArray();

            foreach (var key in keys)
            {
                var itemPath = path.Append(new DictionaryKeySegment(key));
                if (!ValidateDictionaryKey(schema, itemPath, key))
                {
                    continue;
                }

                var hasIncoming = incomingObject.ContainsKey(key);
                Visit(valueSchema, itemPath, originalObject?[key], incomingObject[key], !hasIncoming);
            }
        }

        private void VisitList(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming)
        {
            if (incoming is not JsonArray incomingArray)
            {
                AddIssue(schema, path, localizer["ImportExport:Diagnostics:ExpectedArray"], DisplayJson(incoming, schema));
                return;
            }

            if (schema.ListTemplate is not { } listTemplate)
            {
                AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, path, localizer["ImportExport:Diagnostics:UnsupportedSchema"]);
                return;
            }

            if (!listTemplate.SupportsPerItemMutation)
            {
                VisitWholeContainer(schema, path, original, incoming);
                return;
            }

            VisitKeyedList(schema, path, original as JsonArray, incomingArray, listTemplate);
        }

        private void VisitKeyedList(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonArray? originalArray,
            JsonArray incomingArray,
            ConfigurationListTemplate listTemplate)
        {
            var itemSchema = listTemplate.ItemTemplate;
            var keyName = listTemplate.ItemKeyPropertyName!;
            var originalItems = BuildKeyedItems(originalArray, keyName, path, reportDiagnostics: false);
            var incomingItems = BuildKeyedItems(incomingArray, keyName, path, reportDiagnostics: true);
            var keys = incomingItems.Keys
                .Concat(originalItems.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToArray();

            foreach (var key in keys)
            {
                var itemPath = path.Append(new ListItemKeySegment(key));
                incomingItems.TryGetValue(key, out var incomingItem);
                originalItems.TryGetValue(key, out var originalItem);
                Visit(itemSchema, itemPath, originalItem, incomingItem, !incomingItems.ContainsKey(key));
            }
        }

        private Dictionary<string, JsonNode?> BuildKeyedItems(
            JsonArray? array,
            string keyName,
            LogicalPath listPath,
            bool reportDiagnostics)
        {
            var items = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            if (array is null)
            {
                return items;
            }

            for (var index = 0; index < array.Count; index++)
            {
                var item = array[index];
                var key = ReadObjectScalar(item, keyName);
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (reportDiagnostics)
                    {
                        AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, listPath.Append(new ListIndexSegment(index)),
                            localizer["ImportExport:Diagnostics:MissingListKey", keyName]);
                    }

                    continue;
                }

                if (!items.TryAdd(key, item) && reportDiagnostics)
                {
                    AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, listPath.Append(new ListIndexSegment(index)),
                        localizer["ImportExport:Diagnostics:DuplicateListKey", key]);
                }
            }

            return items;
        }

        private void VisitWholeContainer(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming)
        {
            if (JsonEquivalent(original, incoming))
            {
                _unchangedCount++;
                return;
            }

            StageSet(schema, path, original, incoming);
        }

        private void VisitScalar(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming)
        {
            var conversion = TryConvertScalar(schema, incoming);
            if (!conversion.IsValid)
            {
                AddIssue(schema, path, conversion.Error ?? localizer["State:Editor:InvalidPattern"], conversion.DisplayValue);
                return;
            }

            var validationError = ValidateScalar(schema, conversion.DisplayValue ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                AddIssue(schema, path, validationError, conversion.DisplayValue);
                return;
            }

            var originalConversion = TryConvertScalar(schema, original);
            if (originalConversion.IsValid && StoredJsonEquals(originalConversion.StoredValue?.Json, conversion.StoredValue?.Json))
            {
                _unchangedCount++;
                return;
            }

            StageSet(schema, path, original, incoming, conversion);
        }

        private void StageSet(
            ConfigurationNodeDefinition schema,
            LogicalPath path,
            JsonNode? original,
            JsonNode? incoming,
            ScalarConversion? scalarConversion = null)
        {
            var source = ResolveTargetSource(path);
            if (source is { Kind: not ConfigurationSourceKind.MonicaEffectiveStore, IsWritable: false })
            {
                AddIssue(schema, path,
                    localizer["ImportExport:Diagnostics:ReadOnlySource", source.DisplayName, source.ReadOnlyReason ?? localizer["Sources:ReadOnly"]],
                    scalarConversion?.DisplayValue ?? DisplayJson(incoming, schema));
                return;
            }

            var newValue = scalarConversion?.StoredValue ?? ToStoredValue(incoming);
            var change = new PendingChange
            {
                DefinitionKey = request.Definition.DefinitionKey,
                DefinitionDisplayName = request.Definition.DisplayName,
                LogicalPath = path,
                NodeDisplayName = DisplayName(schema),
                MutationKind = ConfigurationMutationKind.Set,
                NewValue = newValue ?? ConfigurationStoredValue.Null,
                OriginalValue = original is null ? null : ToStoredValue(original),
                OriginalDisplayValue = DisplayJson(original, schema),
                NewDisplayValue = scalarConversion?.DisplayValue ?? DisplayJson(incoming, schema),
                ExpectedSchemaVersion = request.Definition.SchemaVersion,
                ExpectedValueVersion = source?.Kind == ConfigurationSourceKind.MonicaEffectiveStore
                    ? EffectiveValueFor(path)?.Version ?? request.EffectiveValue.Version
                    : null,
                IsSensitive = schema.IsSensitive,
                NodeKind = schema.NodeKind,
                ValueKind = schema.ValueKind,
                ReloadBehavior = EffectiveReloadBehaviorFor(schema)
            }.WithTarget(request.Definition, source);
            _changes.Add(change);
        }

        private void StageRemove(ConfigurationNodeDefinition schema, LogicalPath path, JsonNode? original)
        {
            var source = ResolveTargetSource(path);
            if (source is { Kind: not ConfigurationSourceKind.MonicaEffectiveStore, IsWritable: false })
            {
                AddIssue(schema, path,
                    localizer["ImportExport:Diagnostics:ReadOnlySource", source.DisplayName, source.ReadOnlyReason ?? localizer["Sources:ReadOnly"]],
                    null);
                return;
            }

            _changes.Add(new PendingChange
            {
                DefinitionKey = request.Definition.DefinitionKey,
                DefinitionDisplayName = request.Definition.DisplayName,
                LogicalPath = path,
                NodeDisplayName = DisplayName(schema),
                MutationKind = ConfigurationMutationKind.Remove,
                NewValue = ConfigurationStoredValue.Null,
                OriginalValue = original is null ? null : ToStoredValue(original),
                OriginalDisplayValue = DisplayJson(original, schema),
                NewDisplayValue = localizer["Mutation:Kinds:Remove"],
                ExpectedSchemaVersion = request.Definition.SchemaVersion,
                ExpectedValueVersion = source?.Kind == ConfigurationSourceKind.MonicaEffectiveStore
                    ? EffectiveValueFor(path)?.Version ?? request.EffectiveValue.Version
                    : null,
                IsSensitive = schema.IsSensitive,
                NodeKind = schema.NodeKind,
                ValueKind = schema.ValueKind,
                ReloadBehavior = EffectiveReloadBehaviorFor(schema)
            }.WithTarget(request.Definition, source));
        }

        private ConfigurationSourceDescriptor? ResolveTargetSource(LogicalPath path)
        {
            var exact = EffectiveValueFor(path)?.EffectiveSource;
            if (exact is not null)
            {
                return exact;
            }

            var candidates = request.ScalarEffectiveValues
                .Where(pair => IsPrefix(path, pair.Key))
                .Select(pair => pair.Value.EffectiveSource)
                .Where(static source => source is not null)
                .DistinctBy(static source => source!.SourceKey, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 1)
            {
                return candidates[0];
            }

            var scopeCandidates = request.ScalarEffectiveValues
                .Where(pair => IsPrefix(request.ScopeNode.RelativePath, pair.Key))
                .Select(pair => pair.Value.EffectiveSource)
                .Where(static source => source is not null)
                .DistinctBy(static source => source!.SourceKey, StringComparer.Ordinal)
                .ToArray();
            return scopeCandidates.Length == 1 ? scopeCandidates[0] : null;
        }

        private ConfigurationEffectiveValue? EffectiveValueFor(LogicalPath path)
        {
            return request.ScalarEffectiveValues.GetValueOrDefault(path);
        }

        private bool ShouldSkipRedacted(LogicalPath path)
        {
            var canonical = path.ToCanonicalString();
            return request.RedactedPaths.Contains(canonical)
                   || request.RedactedPaths.Any(redacted =>
                       string.Equals(redacted, canonical, StringComparison.Ordinal)
                       || canonical.StartsWith($"{redacted}.", StringComparison.Ordinal));
        }

        private void ReportUnknownProperties(ConfigurationNodeDefinition schema, LogicalPath path, JsonObject incomingObject)
        {
            var known = schema.Children.Select(child => child.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var propertyName in incomingObject.Select(pair => pair.Key).Where(propertyName => !known.Contains(propertyName)))
            {
                AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, path.Append(new PropertySegment(propertyName)),
                    localizer["ImportExport:Diagnostics:UnknownPath", path.Append(new PropertySegment(propertyName)).ToCanonicalString()]);
            }
        }

        private bool ValidateDictionaryKey(ConfigurationNodeDefinition schema, LogicalPath path, string key)
        {
            var template = schema.DictionaryTemplate;
            if (template is null)
            {
                return true;
            }

            if (template.DisallowColonInKey && key.Contains(':', StringComparison.Ordinal))
            {
                AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, path, localizer["Dialogs:ComplexEditor:Errors:ColonNotAllowed"]);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(template.KeyRegexPattern) && !Regex.IsMatch(key, template.KeyRegexPattern))
            {
                AddDiagnostic(ConfigurationImportDiagnosticSeverity.Warning, path, localizer["State:Editor:InvalidPattern"]);
                return false;
            }

            return true;
        }

        private ScalarConversion TryConvertScalar(ConfigurationNodeDefinition schema, JsonNode? value)
        {
            if (value is null)
            {
                return ScalarConversion.Valid(ConfigurationStoredValue.Null, string.Empty);
            }

            using var document = JsonDocument.Parse(value.ToJsonString());
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Null)
            {
                return ScalarConversion.Valid(ConfigurationStoredValue.Null, string.Empty);
            }

            return schema.ValueKind switch
            {
                ConfigurationValueKind.Boolean => ConvertBoolean(element),
                ConfigurationValueKind.Integer => ConvertInteger(element),
                ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating => ConvertDecimal(element),
                ConfigurationValueKind.DateTime => ConvertDateTime(element),
                ConfigurationValueKind.TimeSpan => ConvertTimeSpan(element),
                ConfigurationValueKind.Enum => ConvertEnum(schema, element),
                ConfigurationValueKind.Json => ScalarConversion.Valid(ConfigurationStoredValue.FromJson(value.ToJsonString()), DisplayJson(value, schema)),
                _ => ConvertStringLike(element)
            };
        }

        private ScalarConversion ConvertBoolean(JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var value = element.GetBoolean();
                return ScalarConversion.Valid(ConfigurationStoredValue.FromJson(value ? "true" : "false"), value ? "true" : "false");
            }

            if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed))
            {
                return ScalarConversion.Valid(ConfigurationStoredValue.FromJson(parsed ? "true" : "false"), parsed ? "true" : "false");
            }

            return ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedBoolean"], element.GetRawText());
        }

        private ScalarConversion ConvertInteger(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var integer))
            {
                return ScalarConversion.Valid(
                    ConfigurationStoredValue.FromJson(integer.ToString(CultureInfo.InvariantCulture)),
                    integer.ToString(CultureInfo.InvariantCulture));
            }

            if (element.ValueKind == JsonValueKind.String
                && long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return ScalarConversion.Valid(
                    ConfigurationStoredValue.FromJson(parsed.ToString(CultureInfo.InvariantCulture)),
                    parsed.ToString(CultureInfo.InvariantCulture));
            }

            return ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedInteger"], element.GetRawText());
        }

        private ScalarConversion ConvertDecimal(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            {
                return ScalarConversion.Valid(
                    ConfigurationStoredValue.FromJson(number.ToString(CultureInfo.InvariantCulture)),
                    number.ToString(CultureInfo.InvariantCulture));
            }

            if (element.ValueKind == JsonValueKind.String
                && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return ScalarConversion.Valid(
                    ConfigurationStoredValue.FromJson(parsed.ToString(CultureInfo.InvariantCulture)),
                    parsed.ToString(CultureInfo.InvariantCulture));
            }

            return ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedNumber"], element.GetRawText());
        }

        private ScalarConversion ConvertDateTime(JsonElement element)
        {
            var text = ReadStringLike(element);
            if (!string.IsNullOrWhiteSpace(text)
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value))
            {
                var display = value.ToString("O", CultureInfo.InvariantCulture);
                return ScalarConversion.Valid(ConfigurationStoredValue.FromJson(JsonSerializer.Serialize(display)), display);
            }

            return ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedDateTime"], text ?? element.GetRawText());
        }

        private ScalarConversion ConvertTimeSpan(JsonElement element)
        {
            var text = ReadStringLike(element);
            if (!string.IsNullOrWhiteSpace(text) && ConfigurationScalarTextCodec.TryParseTimeSpan(text, out var value))
            {
                var display = ConfigurationScalarTextCodec.FormatTimeSpan(value);
                return ScalarConversion.Valid(
                    ConfigurationStoredValue.FromJson(ConfigurationScalarTextCodec.ToTimeSpanJson(value)),
                    display);
            }

            return ScalarConversion.Invalid(localizer["State:Editor:InvalidTimeSpan"], text ?? element.GetRawText());
        }

        private ScalarConversion ConvertEnum(ConfigurationNodeDefinition schema, JsonElement element)
        {
            var text = ReadStringLike(element);
            if (text is null)
            {
                return ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedScalar"], element.GetRawText());
            }

            var result = ConfigurationScalarValueCodec.ConvertDisplayValue(schema, text, localizer);
            return result is { IsValid: true, StoredValue: not null }
                ? ScalarConversion.Valid(result.StoredValue, result.DisplayValue)
                : ScalarConversion.Invalid(result.ValidationError ?? localizer["State:Editor:InvalidPattern"], text);
        }

        private ScalarConversion ConvertStringLike(JsonElement element)
        {
            var text = ReadStringLike(element);
            return text is null
                ? ScalarConversion.Invalid(localizer["ImportExport:Diagnostics:ExpectedScalar"], element.GetRawText())
                : ScalarConversion.Valid(ConfigurationStoredValue.FromJson(JsonSerializer.Serialize(text)), text);
        }

        private JsonNode? NormalizeJsonNode(ConfigurationNodeDefinition schema, JsonNode? node)
        {
            if (node is null)
            {
                return null;
            }

            return schema.NodeKind switch
            {
                ConfigurationNodeKind.Scalar => NormalizeScalarNode(schema, node),
                ConfigurationNodeKind.Object => NormalizeObjectNode(schema, node),
                ConfigurationNodeKind.Dictionary => NormalizeDictionaryNode(schema, node),
                ConfigurationNodeKind.List => NormalizeListNode(schema, node),
                _ => CloneNode(node)
            };
        }

        private JsonNode? NormalizeScalarNode(ConfigurationNodeDefinition schema, JsonNode node)
        {
            var conversion = TryConvertScalar(schema, node);
            return conversion is { IsValid: true, StoredValue: not null }
                ? JsonNode.Parse(conversion.StoredValue.Json)
                : CloneNode(node);
        }

        private JsonNode? NormalizeObjectNode(ConfigurationNodeDefinition schema, JsonNode node)
        {
            if (node is not JsonObject source)
            {
                return CloneNode(node);
            }

            var normalized = new JsonObject();
            foreach (var property in source)
            {
                var childSchema = schema.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Key, StringComparison.Ordinal));
                normalized[property.Key] = childSchema is null
                    ? CloneNode(property.Value)
                    : NormalizeJsonNode(childSchema, property.Value);
            }

            return normalized;
        }

        private JsonNode? NormalizeDictionaryNode(ConfigurationNodeDefinition schema, JsonNode node)
        {
            if (node is not JsonObject source || schema.DictionaryTemplate?.ValueTemplate is not { } valueSchema)
            {
                return CloneNode(node);
            }

            var normalized = new JsonObject();
            foreach (var property in source)
            {
                normalized[property.Key] = NormalizeJsonNode(valueSchema, property.Value);
            }

            return normalized;
        }

        private JsonNode? NormalizeListNode(ConfigurationNodeDefinition schema, JsonNode node)
        {
            if (node is not JsonArray source || schema.ListTemplate?.ItemTemplate is not { } itemSchema)
            {
                return CloneNode(node);
            }

            var normalized = new JsonArray();
            foreach (var item in source)
            {
                normalized.Add(NormalizeJsonNode(itemSchema, item));
            }

            return normalized;
        }

        private static string? ReadStringLike(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private string? ValidateScalar(ConfigurationNodeDefinition schema, string value)
        {
            if (schema.ValidationRules.OfType<RequiredRule>().Any() && string.IsNullOrWhiteSpace(value))
            {
                return localizer["State:Editor:Required"];
            }

            foreach (var rule in schema.ValidationRules)
            {
                switch (rule)
                {
                    case AllowedValuesRule allowedValuesRule when !string.IsNullOrWhiteSpace(value)
                                                                  && !allowedValuesRule.Values.Contains(value, StringComparer.OrdinalIgnoreCase):
                        return rule.ErrorMessage ?? localizer["State:Editor:InvalidPattern"];
                    case RegexRule regexRule when !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, regexRule.Pattern):
                        return rule.ErrorMessage ?? localizer["State:Editor:InvalidPattern"];
                    case RangeRule rangeRule when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue):
                        if (rangeRule.Min is not null && decimalValue < rangeRule.Min || rangeRule.Max is not null && decimalValue > rangeRule.Max)
                        {
                            return rule.ErrorMessage ?? localizer["State:Editor:OutOfRange"];
                        }

                        break;
                    case MaxLengthRule maxLengthRule when value.Length > maxLengthRule.Max:
                        return rule.ErrorMessage ?? localizer["State:Editor:TooLong"];
                    case MinLengthRule minLengthRule when value.Length < minLengthRule.Min:
                        return rule.ErrorMessage ?? localizer["State:Editor:TooShort"];
                }
            }

            return null;
        }

        private void AddIssue(ConfigurationNodeDefinition schema, LogicalPath path, string error, string? invalidDisplayValue)
        {
            _issues.Add(new ConfigurationValidationIssue
            {
                DefinitionKey = request.Definition.DefinitionKey,
                DefinitionDisplayName = request.Definition.DisplayName,
                LogicalPath = path,
                NodeDisplayName = DisplayName(schema),
                InvalidDisplayValue = schema.IsSensitive ? localizer["State:Value:Sensitive"] : invalidDisplayValue,
                ValidationError = error,
                IsSensitive = schema.IsSensitive,
                ValidationRules = schema.ValidationRules
            });
        }

        private void AddDiagnostic(ConfigurationImportDiagnosticSeverity severity, LogicalPath path, string message)
        {
            _diagnostics.Add(Diagnostic(severity, path, message));
        }

        private ConfigurationImportDiagnostic Diagnostic(ConfigurationImportDiagnosticSeverity severity, LogicalPath path, string message)
        {
            return new ConfigurationImportDiagnostic
            {
                Severity = severity,
                DefinitionKey = request.Definition.DefinitionKey,
                LogicalPath = path,
                Message = message
            };
        }

        private string DisplayName(ConfigurationNodeDefinition schema)
        {
            if (schema.RelativePath.Depth == 0)
            {
                return localizer["State:Tree:Root"];
            }

            return string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.Name : schema.DisplayName!;
        }

        private string DisplayJson(JsonNode? node, ConfigurationNodeDefinition schema)
        {
            return ConfigurationJsonDisplayFormatter.Format(node, schema, localizer["State:Value:Sensitive"]);
        }

        private ConfigurationStoredValue? ToStoredValue(JsonNode? node)
        {
            return node is null
                ? ConfigurationStoredValue.Null
                : ConfigurationStoredValue.FromJson(node.ToJsonString());
        }

        private ConfigurationReloadBehavior EffectiveReloadBehaviorFor(ConfigurationNodeDefinition schema)
        {
            return schema.ResolveEffectiveReloadBehavior(request.Definition);
        }

        private static JsonNode? ParseOriginal(string? json, ConfigurationNodeDefinition schema)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(schema);
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

        private static string? ReadObjectScalar(JsonNode? item, string propertyName)
        {
            if (item is not JsonObject itemObject)
            {
                return null;
            }

            var value = itemObject[propertyName];
            if (value is null)
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

        private static bool StoredJsonEquals(string? left, string? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            return JsonEquivalent(JsonNode.Parse(left), JsonNode.Parse(right));
        }

        private static bool JsonEquivalent(JsonNode? left, JsonNode? right)
        {
            return string.Equals(left?.ToJsonString() ?? "null", right?.ToJsonString() ?? "null", StringComparison.Ordinal);
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
    }

    private sealed record ScalarConversion(
        bool IsValid,
        ConfigurationStoredValue? StoredValue,
        string? DisplayValue,
        string? Error)
    {
        public static ScalarConversion Valid(ConfigurationStoredValue storedValue, string displayValue)
        {
            return new ScalarConversion(true, storedValue, displayValue, null);
        }

        public static ScalarConversion Invalid(string error, string? displayValue)
        {
            return new ScalarConversion(false, null, displayValue, error);
        }
    }

    private sealed record DuplicateJsonProperty(string Name, LogicalPath Path);
}

/// <summary>
/// JSON editor document plus metadata about redacted paths.
/// </summary>
/// <param name="Json">The editor JSON text.</param>
/// <param name="RedactedPaths">The canonical paths that were redacted in the editor JSON.</param>
internal sealed record ConfigurationJsonEditorDocument(string Json, IReadOnlyList<string> RedactedPaths);
