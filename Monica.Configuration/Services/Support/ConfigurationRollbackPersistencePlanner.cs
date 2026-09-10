using System.Globalization;
using System.Text.Json;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Converts effective-value rollback differences into source-bound mutations and rejects runtime/source drift.
/// </summary>
internal sealed class ConfigurationRollbackPersistencePlanner(
    IConfigurationSourceInspector sourceInspector,
    IConfigurationJsonFileSourceWriter sourceWriter,
    ConfigurationEffectiveValueDocumentEditor documentEditor,
    ConfigurationPathProjector pathProjector,
    MonicaConfigurationProviderAccessor providerAccessor)
{
    private const string EFFECTIVE_SOURCE_KEY = "monica:effective";
    private const string EFFECTIVE_SOURCE_DISPLAY_NAME = "Monica Effective Store";

    public async Task<IReadOnlyList<ConfigurationUnifiedVersionApplyMutation>> PlanAsync(
        ConfigurationDefinition definition,
        ConfigurationResolvedEffectiveSnapshot currentSnapshot,
        string targetJson,
        CancellationToken cancellationToken)
    {
        var changes = ConfigurationRollbackChangePlanner.Plan(definition.Root, currentSnapshot.Json, targetJson);
        return await ResolveMutationDestinationsAsync(definition, currentSnapshot, changes, cancellationToken);
    }

    /// <summary>
    /// Plans a published-only definition directly against its shared effective-value document.
    /// </summary>
    /// <remarks>
    /// The current JSON and expected version must come from the same store read. Consulting the local runtime source
    /// chain would be incorrect because this process did not scan or project the remote definition.
    /// </remarks>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyMutation> PlanEffectiveStore(
        ConfigurationDefinition definition,
        string currentJson,
        string targetJson,
        long expectedValueVersion)
    {
        return ConfigurationRollbackChangePlanner.Plan(definition.Root, currentJson, targetJson)
            .Select(change => new ConfigurationUnifiedVersionApplyMutation
            {
                LogicalPath = change.LogicalPath.ToCanonicalString(),
                ConfigurationPath = pathProjector.Project(definition.SectionPath, change.LogicalPath),
                MutationKind = change.MutationKind,
                CurrentJson = change.CurrentJson,
                TargetJson = change.TargetJson,
                SourceKey = EFFECTIVE_SOURCE_KEY,
                SourceDisplayName = EFFECTIVE_SOURCE_DISPLAY_NAME,
                SourceKind = ConfigurationSourceKind.MonicaEffectiveStore,
                ExpectedValueVersion = expectedValueVersion
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<ConfigurationUnifiedVersionApplyMutation>> FindRuntimeSourceDriftsAsync(
        ConfigurationDefinition definition,
        ConfigurationResolvedEffectiveSnapshot currentSnapshot,
        CancellationToken cancellationToken)
    {
        var candidates = ConfigurationRollbackChangePlanner.EnumerateLeaves(definition.Root, currentSnapshot.Json)
            .Select(leaf => CreateDriftCandidate(definition, leaf))
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .ToArray();
        var drifts = new List<ConfigurationUnifiedVersionApplyMutation>();
        var sources = sourceInspector.GetSources();

        FindMonicaDrifts(
            definition,
            candidates,
            sources,
            drifts,
            currentSnapshot.PersistedDocument);
        await FindJsonSourceDriftsAsync(definition, candidates, sources, drifts, cancellationToken);
        return drifts;
    }

    private PersistenceDriftCandidate? CreateDriftCandidate(
        ConfigurationDefinition definition,
        ConfigurationRollbackLeafValue leaf)
    {
        var sourceChain = sourceInspector.GetSourceChain(definition, leaf.LogicalPath);
        var source = sourceChain.Values.FirstOrDefault(static value => value.IsEffective)?.Source;
        return source is not null
               && (source.IsWritable
                   || source.Kind == ConfigurationSourceKind.JsonFile
                   && !string.IsNullOrWhiteSpace(source.PhysicalPath))
            ? new PersistenceDriftCandidate(leaf, source, sourceChain.ConfigurationPath)
            : null;
    }

    private void FindMonicaDrifts(
        ConfigurationDefinition definition,
        IReadOnlyList<PersistenceDriftCandidate> candidates,
        IReadOnlyList<ConfigurationSourceDescriptor> sources,
        ICollection<ConfigurationUnifiedVersionApplyMutation> drifts,
        ConfigurationEffectiveValueDocument? document)
    {
        var loadedVersion = providerAccessor.Provider?.GetLoadedVersion(definition.DefinitionKey);
        if (loadedVersion is not null && (document is null || document.Version < loadedVersion))
        {
            if (sources.FirstOrDefault(static source =>
                    source.Kind == ConfigurationSourceKind.MonicaEffectiveStore) is { } monicaSource)
            {
                drifts.Add(CreateSourceProjectionDriftMutation(
                    definition,
                    monicaSource,
                    expectedValueVersion: document?.Version ?? 0,
                    expectedSourceRevision: null));
            }

            return;
        }

        if (document is null)
        {
            return;
        }

        foreach (var candidate in candidates.Where(static candidate =>
                     candidate.Source.Kind == ConfigurationSourceKind.MonicaEffectiveStore))
        {
            var storedValue = documentEditor.ReadValue(
                definition,
                document.Json,
                candidate.Leaf.LogicalPath);
            if (!StoredValueMatches(storedValue, candidate.Leaf))
            {
                drifts.Add(CreatePersistenceDriftMutation(
                    candidate,
                    storedValue,
                    expectedValueVersion: document.Version,
                    expectedSourceRevision: null));
            }
        }
    }

    private async Task FindJsonSourceDriftsAsync(
        ConfigurationDefinition definition,
        IReadOnlyList<PersistenceDriftCandidate> candidates,
        IReadOnlyList<ConfigurationSourceDescriptor> sources,
        ICollection<ConfigurationUnifiedVersionApplyMutation> drifts,
        CancellationToken cancellationToken)
    {
        var candidatesBySource = candidates
            .Where(static candidate => candidate.Source.Kind == ConfigurationSourceKind.JsonFile)
            .GroupBy(static candidate => candidate.Source.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Where(static source =>
                     source.Kind == ConfigurationSourceKind.JsonFile
                     && !string.IsNullOrWhiteSpace(source.PhysicalPath)))
        {
            var sourceCandidates = candidatesBySource.GetValueOrDefault(source.SourceKey) ?? [];
            var snapshot = await sourceWriter.ReadValuesAsync(
                source,
                definition,
                sourceCandidates.Select(static candidate => candidate.ConfigurationPath).ToArray(),
                cancellationToken);
            var runtimeProjectionRevision = sourceInspector.GetRuntimeProjectionRevision(
                definition,
                source.SourceKey);
            if (!string.Equals(
                    snapshot.ProjectionRevision,
                    runtimeProjectionRevision,
                    StringComparison.OrdinalIgnoreCase))
            {
                drifts.Add(CreateSourceProjectionDriftMutation(
                    definition,
                    source,
                    expectedValueVersion: null,
                    expectedSourceRevision: snapshot.Revision));
                continue;
            }

            foreach (var candidate in sourceCandidates)
            {
                snapshot.Values.TryGetValue(candidate.ConfigurationPath, out var storedValue);
                if (!StoredValueMatches(storedValue, candidate.Leaf))
                {
                    drifts.Add(CreatePersistenceDriftMutation(
                        candidate,
                        storedValue,
                        expectedValueVersion: null,
                        expectedSourceRevision: snapshot.Revision));
                }
            }
        }
    }

    private async Task<IReadOnlyList<ConfigurationUnifiedVersionApplyMutation>> ResolveMutationDestinationsAsync(
        ConfigurationDefinition definition,
        ConfigurationResolvedEffectiveSnapshot currentSnapshot,
        IReadOnlyList<ConfigurationRollbackValueChange> changes,
        CancellationToken cancellationToken)
    {
        var sources = sourceInspector.GetSources();
        var monicaSource = sources.FirstOrDefault(static source =>
            source.Kind == ConfigurationSourceKind.MonicaEffectiveStore);
        var sourceByKey = sources.ToDictionary(static source => source.SourceKey, StringComparer.OrdinalIgnoreCase);
        var mutations = changes
            .Select(change => ResolveMutationDestination(definition, change, monicaSource))
            .ToArray();

        BindMonicaConcurrency(definition, currentSnapshot, changes, mutations);
        await BindJsonSourceConcurrencyAsync(definition, changes, mutations, sourceByKey, cancellationToken);
        return mutations;
    }

    private void BindMonicaConcurrency(
        ConfigurationDefinition definition,
        ConfigurationResolvedEffectiveSnapshot currentSnapshot,
        IReadOnlyList<ConfigurationRollbackValueChange> changes,
        ConfigurationUnifiedVersionApplyMutation[] mutations)
    {
        if (!mutations.Any(static mutation =>
                mutation.Status == ConfigurationUnifiedVersionApplyMutationStatus.Ready
                && mutation.SourceKind == ConfigurationSourceKind.MonicaEffectiveStore))
        {
            return;
        }

        // Use the document captured with the reviewed runtime JSON. Re-reading here could mix generations inside one
        // preview and would turn rollback planning back into a per-definition store query.
        var document = currentSnapshot.PersistedDocument;
        var expectedVersion = document?.Version ?? 0;
        var loadedVersion = providerAccessor.Provider?.GetLoadedVersion(definition.DefinitionKey);
        var generationDrift = loadedVersion is not null
                              && (document is null || document.Version < loadedVersion);
        for (var index = 0; index < mutations.Length; index++)
        {
            if (mutations[index] is not
                {
                    Status: ConfigurationUnifiedVersionApplyMutationStatus.Ready,
                    SourceKind: ConfigurationSourceKind.MonicaEffectiveStore
                })
            {
                continue;
            }

            var change = changes[index];
            var sourceOwnsCurrentValue = SourceOwnsCurrentValue(
                definition,
                change.LogicalPath,
                mutations[index].SourceKey);
            var storedValue = document is null || !sourceOwnsCurrentValue
                ? null
                : documentEditor.ReadValue(definition, document.Json, change.LogicalPath);
            var valueDrift = document is not null
                             && sourceOwnsCurrentValue
                             && !StoredValueMatches(storedValue, change.CurrentJson, change.Schema);
            mutations[index] = generationDrift || valueDrift
                ? CreateRuntimeDriftMutation(
                    mutations[index],
                    change,
                    storedValue,
                    expectedValueVersion: expectedVersion,
                    expectedSourceRevision: null)
                : mutations[index] with { ExpectedValueVersion = expectedVersion };
        }
    }

    private async Task BindJsonSourceConcurrencyAsync(
        ConfigurationDefinition definition,
        IReadOnlyList<ConfigurationRollbackValueChange> changes,
        ConfigurationUnifiedVersionApplyMutation[] mutations,
        IReadOnlyDictionary<string, ConfigurationSourceDescriptor> sourceByKey,
        CancellationToken cancellationToken)
    {
        foreach (var sourceGroup in mutations
                     .Where(static mutation => mutation is
                     {
                         Status: ConfigurationUnifiedVersionApplyMutationStatus.Ready,
                         SourceKind: ConfigurationSourceKind.JsonFile,
                         SourceKey.Length: > 0,
                         ConfigurationPath.Length: > 0
                     })
                     .GroupBy(static mutation => mutation.SourceKey!, StringComparer.OrdinalIgnoreCase))
        {
            var source = sourceByKey[sourceGroup.Key];
            var snapshot = await sourceWriter.ReadPhysicalValuesAsync(
                source,
                sourceGroup.Select(static mutation => mutation.ConfigurationPath!).ToArray(),
                cancellationToken);
            for (var index = 0; index < mutations.Length; index++)
            {
                if (mutations[index].Status != ConfigurationUnifiedVersionApplyMutationStatus.Ready
                    || !string.Equals(
                        mutations[index].SourceKey,
                        sourceGroup.Key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var change = changes[index];
                snapshot.Values.TryGetValue(mutations[index].ConfigurationPath!, out var storedValue);
                mutations[index] = StoredValueMatches(storedValue, change.CurrentJson, change.Schema)
                    ? mutations[index] with { ExpectedSourceRevision = snapshot.Revision }
                    : CreateRuntimeDriftMutation(
                        mutations[index],
                        change,
                        storedValue,
                        expectedValueVersion: null,
                        expectedSourceRevision: snapshot.Revision);
            }
        }
    }

    private ConfigurationUnifiedVersionApplyMutation ResolveMutationDestination(
        ConfigurationDefinition definition,
        ConfigurationRollbackValueChange change,
        ConfigurationSourceDescriptor? monicaSource)
    {
        var sourceChain = sourceInspector.GetSourceChain(definition, change.LogicalPath);
        var effectiveValue = sourceChain.Values.FirstOrDefault(static value => value.IsEffective);
        var mutation = new ConfigurationUnifiedVersionApplyMutation
        {
            LogicalPath = change.LogicalPath.ToCanonicalString(),
            ConfigurationPath = sourceChain.ConfigurationPath,
            MutationKind = change.MutationKind,
            CurrentJson = change.CurrentJson,
            TargetJson = change.TargetJson,
            ExpectedSourceChainRevision = ConfigurationSourceChainRevision.Compute(sourceChain)
        };

        if (effectiveValue is { Source.IsWritable: false })
        {
            return mutation with
            {
                BlockingSourceDisplayName = effectiveValue.Source.DisplayName,
                Status = ConfigurationUnifiedVersionApplyMutationStatus.ReadOnlyOverride
            };
        }

        if (change.MutationKind == ConfigurationMutationKind.Set
            && change.Schema.NodeKind != ConfigurationNodeKind.Scalar
            && sourceChain.Values.Count > 1)
        {
            return mutation with
            {
                SourceKey = effectiveValue?.Source.SourceKey,
                SourceDisplayName = effectiveValue?.Source.DisplayName,
                SourceKind = effectiveValue?.Source.Kind,
                BlockingSourceDisplayName = sourceChain.Values
                    .First(value => !ReferenceEquals(value, effectiveValue))
                    .Source.DisplayName,
                Status = ConfigurationUnifiedVersionApplyMutationStatus.CompositeSourceConflict
            };
        }

        if (change.MutationKind == ConfigurationMutationKind.Remove
            && effectiveValue is not null
            && sourceChain.Values.FirstOrDefault(value =>
                value.Source.PriorityIndex < effectiveValue.Source.PriorityIndex) is { } fallbackValue)
        {
            return mutation with
            {
                SourceKey = effectiveValue.Source.SourceKey,
                SourceDisplayName = effectiveValue.Source.DisplayName,
                SourceKind = effectiveValue.Source.Kind,
                BlockingSourceDisplayName = fallbackValue.Source.DisplayName,
                Status = ConfigurationUnifiedVersionApplyMutationStatus.LowerPriorityFallback
            };
        }

        var source = effectiveValue?.Source ?? monicaSource;
        if (source is null)
        {
            return mutation with
            {
                SourceKey = EFFECTIVE_SOURCE_KEY,
                SourceDisplayName = EFFECTIVE_SOURCE_DISPLAY_NAME,
                SourceKind = ConfigurationSourceKind.MonicaEffectiveStore
            };
        }

        if (!source.IsWritable)
        {
            return mutation with
            {
                BlockingSourceDisplayName = source.DisplayName,
                Status = ConfigurationUnifiedVersionApplyMutationStatus.ReadOnlyOverride
            };
        }

        return source.Kind switch
        {
            ConfigurationSourceKind.MonicaEffectiveStore => mutation with
            {
                SourceKey = source.SourceKey,
                SourceDisplayName = source.DisplayName,
                SourceKind = source.Kind
            },
            ConfigurationSourceKind.JsonFile when !string.IsNullOrWhiteSpace(sourceChain.ConfigurationPath) => mutation with
            {
                SourceKey = source.SourceKey,
                SourceDisplayName = source.DisplayName,
                SourceKind = source.Kind
            },
            _ => mutation with
            {
                SourceKey = source.SourceKey,
                SourceDisplayName = source.DisplayName,
                SourceKind = source.Kind,
                Status = ConfigurationUnifiedVersionApplyMutationStatus.UnsupportedSource
            }
        };
    }

    private bool SourceOwnsCurrentValue(
        ConfigurationDefinition definition,
        LogicalPath logicalPath,
        string? sourceKey)
    {
        return sourceKey is { Length: > 0 }
               && sourceInspector.GetSourceChain(definition, logicalPath).Values.Any(value =>
                   value.IsEffective
                   && string.Equals(value.Source.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
    }

    private static ConfigurationUnifiedVersionApplyMutation CreateSourceProjectionDriftMutation(
        ConfigurationDefinition definition,
        ConfigurationSourceDescriptor source,
        long? expectedValueVersion,
        string? expectedSourceRevision)
    {
        return new ConfigurationUnifiedVersionApplyMutation
        {
            LogicalPath = LogicalPath.Root.ToCanonicalString(),
            ConfigurationPath = definition.SectionPath,
            MutationKind = ConfigurationMutationKind.Set,
            SourceKey = source.SourceKey,
            SourceDisplayName = source.DisplayName,
            SourceKind = source.Kind,
            ExpectedValueVersion = expectedValueVersion,
            ExpectedSourceRevision = expectedSourceRevision,
            Status = ConfigurationUnifiedVersionApplyMutationStatus.RuntimeOutOfSync
        };
    }

    private static ConfigurationUnifiedVersionApplyMutation CreatePersistenceDriftMutation(
        PersistenceDriftCandidate candidate,
        ConfigurationStoredValue? storedValue,
        long? expectedValueVersion,
        string? expectedSourceRevision)
    {
        return new ConfigurationUnifiedVersionApplyMutation
        {
            LogicalPath = candidate.Leaf.LogicalPath.ToCanonicalString(),
            ConfigurationPath = candidate.ConfigurationPath,
            MutationKind = ConfigurationMutationKind.Set,
            CurrentJson = candidate.Leaf.Schema.IsSensitive ? null : storedValue?.Json,
            TargetJson = candidate.Leaf.Json,
            SourceKey = candidate.Source.SourceKey,
            SourceDisplayName = candidate.Source.DisplayName,
            SourceKind = candidate.Source.Kind,
            ExpectedValueVersion = expectedValueVersion,
            ExpectedSourceRevision = expectedSourceRevision,
            Status = ConfigurationUnifiedVersionApplyMutationStatus.RuntimeOutOfSync
        };
    }

    private static ConfigurationUnifiedVersionApplyMutation CreateRuntimeDriftMutation(
        ConfigurationUnifiedVersionApplyMutation mutation,
        ConfigurationRollbackValueChange change,
        ConfigurationStoredValue? storedValue,
        long? expectedValueVersion,
        string? expectedSourceRevision)
    {
        return mutation with
        {
            MutationKind = change.CurrentJson is null
                ? ConfigurationMutationKind.Remove
                : ConfigurationMutationKind.Set,
            CurrentJson = change.Schema.IsSensitive ? null : storedValue?.Json,
            TargetJson = change.CurrentJson,
            ExpectedValueVersion = expectedValueVersion,
            ExpectedSourceRevision = expectedSourceRevision,
            Status = ConfigurationUnifiedVersionApplyMutationStatus.RuntimeOutOfSync
        };
    }

    private static bool StoredValueMatches(
        ConfigurationStoredValue? storedValue,
        ConfigurationRollbackLeafValue target)
    {
        return StoredValueMatches(storedValue, target.Json, target.Schema);
    }

    private static bool StoredValueMatches(
        ConfigurationStoredValue? storedValue,
        string? targetJson,
        ConfigurationNodeDefinition targetSchema)
    {
        if (targetJson is null)
        {
            return storedValue is null;
        }

        if (storedValue is null)
        {
            return false;
        }

        if (ConfigurationJsonSemanticComparer.Equals(storedValue.Json, targetJson))
        {
            return true;
        }

        // Format-only differences (for example TimeSpan or numeric textual variants) fall through to a
        // schema-aware typed comparison so collection nodes do not report false persistence drift.
        using var storedDocument = JsonDocument.Parse(storedValue.Json);
        using var targetDocument = JsonDocument.Parse(targetJson);
        return SchemaAwareEquals(targetSchema, storedDocument.RootElement, targetDocument.RootElement);
    }

    private static bool SchemaAwareEquals(
        ConfigurationNodeDefinition schema,
        JsonElement stored,
        JsonElement target)
    {
        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Scalar => ScalarEquals(schema, stored, target),
            ConfigurationNodeKind.Object => ObjectEquals(schema, stored, target),
            ConfigurationNodeKind.List => ListEquals(schema, stored, target),
            ConfigurationNodeKind.Dictionary => DictionaryEquals(schema, stored, target),
            _ => false
        };
    }

    private static bool ObjectEquals(
        ConfigurationNodeDefinition schema,
        JsonElement stored,
        JsonElement target)
    {
        if (stored.ValueKind != JsonValueKind.Object || target.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Compare only schema-declared children; stored documents may retain properties that the current
        // schema no longer declares, which are irrelevant to runtime projection.
        foreach (var child in schema.Children)
        {
            if (!TryGetProperty(stored, child.Name, out var storedValue)
                || !TryGetProperty(target, child.Name, out var targetValue))
            {
                return false;
            }

            if (!SchemaAwareEquals(child, storedValue, targetValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ListEquals(
        ConfigurationNodeDefinition schema,
        JsonElement stored,
        JsonElement target)
    {
        if (stored.ValueKind != JsonValueKind.Array
            || target.ValueKind != JsonValueKind.Array
            || schema.ListTemplate is null)
        {
            return false;
        }

        var storedItems = stored.EnumerateArray().ToArray();
        var targetItems = target.EnumerateArray().ToArray();
        return storedItems.Length == targetItems.Length
               && storedItems.Zip(targetItems)
                   .All(pair => SchemaAwareEquals(schema.ListTemplate.ItemTemplate, pair.First, pair.Second));
    }

    private static bool DictionaryEquals(
        ConfigurationNodeDefinition schema,
        JsonElement stored,
        JsonElement target)
    {
        if (stored.ValueKind != JsonValueKind.Object
            || target.ValueKind != JsonValueKind.Object
            || schema.DictionaryTemplate is null)
        {
            return false;
        }

        var storedProperties = stored.EnumerateObject().ToArray();
        var targetProperties = target.EnumerateObject().ToArray();
        if (storedProperties.Length != targetProperties.Length)
        {
            return false;
        }

        var targetByKey = targetProperties.ToDictionary(
            static property => property.Name,
            static property => property.Value,
            StringComparer.OrdinalIgnoreCase);
        return storedProperties.All(property =>
            targetByKey.TryGetValue(property.Name, out var targetValue)
            && SchemaAwareEquals(schema.DictionaryTemplate.ValueTemplate, property.Value, targetValue));
    }

    private static bool ScalarEquals(ConfigurationNodeDefinition schema, JsonElement stored, JsonElement target)
    {
        var storedText = ReadConfigurationScalar(stored);
        var targetText = ReadConfigurationScalar(target);
        return schema.ValueKind switch
        {
            ConfigurationValueKind.String =>
                string.Equals(
                    ConfigurationRegexTextCodec.NormalizeDisplayValue(schema, storedText),
                    ConfigurationRegexTextCodec.NormalizeDisplayValue(schema, targetText),
                    StringComparison.Ordinal),
            ConfigurationValueKind.Boolean =>
                bool.TryParse(storedText, out var storedBoolean)
                && bool.TryParse(targetText, out var targetBoolean)
                && storedBoolean == targetBoolean,
            ConfigurationValueKind.Integer or ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating =>
                decimal.TryParse(storedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var storedNumber)
                && decimal.TryParse(targetText, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetNumber)
                && storedNumber == targetNumber,
            ConfigurationValueKind.Enum =>
                schema.TryNormalizeEnumDisplayValue(storedText, out var storedEnum)
                && schema.TryNormalizeEnumDisplayValue(targetText, out var targetEnum)
                && string.Equals(storedEnum, targetEnum, StringComparison.OrdinalIgnoreCase),
            ConfigurationValueKind.DateTime =>
                DateTime.TryParse(
                    storedText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var storedDateTime)
                && DateTime.TryParse(
                    targetText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var targetDateTime)
                && storedDateTime.Equals(targetDateTime),
            ConfigurationValueKind.TimeSpan =>
                TimeSpan.TryParse(storedText, CultureInfo.InvariantCulture, out var storedTimeSpan)
                && TimeSpan.TryParse(targetText, CultureInfo.InvariantCulture, out var targetTimeSpan)
                && storedTimeSpan == targetTimeSpan,
            ConfigurationValueKind.Uri =>
                Uri.TryCreate(storedText, UriKind.RelativeOrAbsolute, out var storedUri)
                && Uri.TryCreate(targetText, UriKind.RelativeOrAbsolute, out var targetUri)
                && storedUri.Equals(targetUri),
            _ => false
        };
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadConfigurationScalar(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private sealed record PersistenceDriftCandidate(
        ConfigurationRollbackLeafValue Leaf,
        ConfigurationSourceDescriptor Source,
        string ConfigurationPath);
}
