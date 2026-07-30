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
        var targets = new List<ConfigurationUnifiedVersionApplyTarget>(snapshot.Definitions.Count);
        foreach (var document in snapshot.Definitions)
        {
            targets.Add(await ResolveTargetAsync(document, cancellationToken));
        }

        return new ConfigurationUnifiedVersionApplyPreview
        {
            Version = snapshot.Summary.Version,
            PreviewFingerprint = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(
                snapshot.Summary.Version,
                targets),
            Targets = targets
        };
    }

    private async Task<ConfigurationUnifiedVersionApplyTarget> ResolveTargetAsync(
        ConfigurationUnifiedVersionDefinitionSnapshot document,
        CancellationToken cancellationToken)
    {
        ConfigurationDefinition definition;
        try
        {
            definition = await definitionResolver.GetRequiredAsync(document.DefinitionKey, cancellationToken);
        }
        catch (KeyNotFoundException)
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

        var currentSnapshot = await effectiveSnapshotReader.ReadAsync(definition, cancellationToken);
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
            : validationCoordinator.ValidateValue(definition, document.Json)
                .Select(issue => CreateValidationIssue(definition, issue, schemaDrift))
                .ToArray();
        if (issues.Length > 0)
        {
            return target with
            {
                Status = ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue,
                ValidationIssues = issues
            };
        }

        var persistenceDrifts = definition.Origin == ConfigurationDefinitionOrigin.LocalScan
            ? await persistencePlanner.FindRuntimeSourceDriftsAsync(
                definition,
                currentJson,
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
                currentJson,
                document.Json,
                cancellationToken);
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
