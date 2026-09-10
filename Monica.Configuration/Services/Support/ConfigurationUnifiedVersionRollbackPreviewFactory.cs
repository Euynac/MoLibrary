using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Builds the complete, source-bound preview used by both unified-version rollback review and apply.
/// </summary>
internal sealed class ConfigurationUnifiedVersionRollbackPreviewFactory(
    ConfigurationDefinitionResolver definitionResolver,
    ConfigurationEffectiveSnapshotReader effectiveSnapshotReader,
    ConfigurationValidationCoordinator validationCoordinator,
    ConfigurationRollbackPersistencePlanner persistencePlanner)
{
    public async Task<ConfigurationUnifiedVersionApplyPreview> CreateAsync(
        ConfigurationUnifiedVersionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Definitions.Count == 0)
        {
            return CreatePreview(snapshot.Summary.Version, []);
        }

        var targetDefinitions = await ResolveTargetDefinitionsAsync(snapshot.Definitions, cancellationToken);
        var effectiveSnapshots = await effectiveSnapshotReader.ReadManyAsync(
            targetDefinitions.OfType<ConfigurationDefinition>().ToArray(),
            cancellationToken);
        var targets = new List<ConfigurationUnifiedVersionApplyTarget>(snapshot.Definitions.Count);
        var effectiveSnapshotIndex = 0;
        for (var index = 0; index < snapshot.Definitions.Count; index++)
        {
            var document = snapshot.Definitions[index];
            var definition = targetDefinitions[index];
            if (definition is null)
            {
                targets.Add(CreateMissingTarget(document));
                continue;
            }

            // Missing historical definitions are excluded from the batch, so only resolved targets advance this index.
            targets.Add(await ResolveTargetAsync(
                document,
                definition,
                effectiveSnapshots[effectiveSnapshotIndex++],
                cancellationToken));
        }

        return CreatePreview(snapshot.Summary.Version, targets);
    }

    private async Task<IReadOnlyList<ConfigurationDefinition?>> ResolveTargetDefinitionsAsync(
        IReadOnlyList<ConfigurationUnifiedVersionDefinitionSnapshot> documents,
        CancellationToken cancellationToken)
    {
        var definitions = new ConfigurationDefinition?[documents.Count];
        for (var index = 0; index < documents.Count; index++)
        {
            try
            {
                definitions[index] = await definitionResolver.GetRequiredAsync(
                    documents[index].DefinitionKey,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                definitions[index] = null;
            }
        }

        return definitions;
    }

    private static ConfigurationUnifiedVersionApplyPreview CreatePreview(
        long version,
        IReadOnlyList<ConfigurationUnifiedVersionApplyTarget> targets)
    {
        return new ConfigurationUnifiedVersionApplyPreview
        {
            Version = version,
            PreviewFingerprint = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(
                version,
                targets),
            Targets = targets
        };
    }

    private static ConfigurationUnifiedVersionApplyTarget CreateMissingTarget(
        ConfigurationUnifiedVersionDefinitionSnapshot document)
    {
        return new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = document.DefinitionKey,
            DisplayName = document.DisplayName,
            TargetJson = document.Json,
            CapturedSchemaHash = document.SchemaHash,
            Status = ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition
        };
    }

    private async Task<ConfigurationUnifiedVersionApplyTarget> ResolveTargetAsync(
        ConfigurationUnifiedVersionDefinitionSnapshot document,
        ConfigurationDefinition definition,
        ConfigurationResolvedEffectiveSnapshot currentSnapshot,
        CancellationToken cancellationToken)
    {
        var currentJson = currentSnapshot.Json;
        var target = new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = document.DefinitionKey,
            DisplayName = document.DisplayName,
            CurrentJson = currentJson,
            TargetJson = document.Json,
            CapturedSchemaHash = document.SchemaHash,
            CurrentSchemaHash = definition.SchemaHash,
            CurrentSchemaVersion = definition.SchemaVersion
        };
        var valuesEqual = ConfigurationJsonSemanticComparer.Equals(currentJson, document.Json);
        var schemaDrift = !string.Equals(definition.SchemaHash, document.SchemaHash, StringComparison.Ordinal);
        var issues = valuesEqual
            ? []
            : validationCoordinator.ValidateCapturedValue(definition, document.Json)
                .Select(issue => CreateValidationIssue(definition, issue, schemaDrift))
                .ToArray();
        if (issues.Length > 0)
        {
            // Hard-incompatible historical values are skipped and reported; they never block the
            // rollback of the remaining definitions.
            return target with
            {
                Status = ConfigurationUnifiedVersionApplyTargetStatus.IncompatibleValue,
                ValidationIssues = issues
            };
        }

        var persistenceDrifts = definition.Origin == ConfigurationDefinitionOrigin.LocalScan
            ? await persistencePlanner.FindRuntimeSourceDriftsAsync(
                definition,
                currentSnapshot,
                cancellationToken)
            : [];
        if (persistenceDrifts.Count > 0)
        {
            return target with
            {
                Mutations = persistenceDrifts,
                Status = ConfigurationUnifiedVersionApplyTargetStatus.RuntimeOutOfSync
            };
        }

        if (valuesEqual)
        {
            return target with { Status = ConfigurationUnifiedVersionApplyTargetStatus.Unchanged };
        }

        var mutations = definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata
            ? persistencePlanner.PlanEffectiveStore(
                definition,
                currentJson,
                document.Json,
                currentSnapshot.RequireVersion(definition))
            : await persistencePlanner.PlanAsync(
                definition,
                currentSnapshot,
                document.Json,
                cancellationToken);
        if (mutations.Count == 0)
        {
            // The semantic difference cannot be written back (for example properties the current schema
            // no longer declares), so the effective configuration does not change.
            return target with { Status = ConfigurationUnifiedVersionApplyTargetStatus.Unchanged };
        }
        var blockedMutation = mutations.FirstOrDefault(static mutation =>
            mutation.Status != ConfigurationUnifiedVersionApplyMutationStatus.Ready);
        if (blockedMutation is not null)
        {
            return target with
            {
                Mutations = mutations,
                Status = blockedMutation.Status switch
                {
                    ConfigurationUnifiedVersionApplyMutationStatus.ReadOnlyOverride =>
                        ConfigurationUnifiedVersionApplyTargetStatus.ReadOnlyOverride,
                    ConfigurationUnifiedVersionApplyMutationStatus.CompositeSourceConflict =>
                        ConfigurationUnifiedVersionApplyTargetStatus.CompositeSourceConflict,
                    ConfigurationUnifiedVersionApplyMutationStatus.LowerPriorityFallback =>
                        ConfigurationUnifiedVersionApplyTargetStatus.LowerPriorityFallback,
                    ConfigurationUnifiedVersionApplyMutationStatus.RuntimeOutOfSync =>
                        ConfigurationUnifiedVersionApplyTargetStatus.RuntimeOutOfSync,
                    _ => ConfigurationUnifiedVersionApplyTargetStatus.UnsupportedSource
                }
            };
        }

        return target with
        {
            Mutations = mutations,
            Status = schemaDrift
                ? ConfigurationUnifiedVersionApplyTargetStatus.CompatibleSchemaDrift
                : ConfigurationUnifiedVersionApplyTargetStatus.Ready
        };
    }

    private static ConfigurationUnifiedVersionValidationIssue CreateValidationIssue(
        ConfigurationDefinition definition,
        ConfigurationValueValidationIssue issue,
        bool schemaDrift)
    {
        var detailsHidden = schemaDrift
                            || ConfigurationSchemaNavigator.IsSensitivePath(definition.Root, issue.LogicalPath);
        return new ConfigurationUnifiedVersionValidationIssue
        {
            LogicalPath = detailsHidden ? string.Empty : issue.LogicalPath.ToCanonicalString(),
            Message = detailsHidden
                ? "Validation details are hidden because the affected historical path may contain sensitive metadata."
                : issue.Message,
            ValidationRules = detailsHidden ? [] : issue.ValidationRules,
            DetailsHidden = detailsHidden
        };
    }

}
