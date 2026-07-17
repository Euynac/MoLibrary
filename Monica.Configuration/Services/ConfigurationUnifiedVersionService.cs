using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

internal sealed class ConfigurationUnifiedVersionService(
    IConfigurationUnifiedVersionStore versionStore,
    IConfigurationMutationGroupApplyService mutationGroupApplyService,
    ConfigurationUnifiedVersionRollbackPreviewFactory rollbackPreviewFactory)
    : IConfigurationUnifiedVersionService
{
    public Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return versionStore.ListVersionsAsync(from, to, definitionKey, limit, cancellationToken);
    }

    public Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(long version, CancellationToken cancellationToken)
    {
        return versionStore.GetVersionAsync(version, cancellationToken);
    }

    public Task DeleteVersionAsync(long version, CancellationToken cancellationToken)
    {
        return versionStore.DeleteVersionAsync(version, cancellationToken);
    }

    public async Task<ConfigurationUnifiedVersionComparison> CompareVersionsAsync(
        long originVersion,
        long targetVersion,
        CancellationToken cancellationToken)
    {
        var origin = await GetRequiredVersionAsync(originVersion, cancellationToken);
        var target = await GetRequiredVersionAsync(targetVersion, cancellationToken);
        var originByKey = origin.Definitions.ToDictionary(
            static definition => definition.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
        var targetByKey = target.Definitions.ToDictionary(
            static definition => definition.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
        var keys = originByKey.Keys.Concat(targetByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase);
        var changes = new List<ConfigurationUnifiedVersionDefinitionChange>();

        foreach (var key in keys)
        {
            originByKey.TryGetValue(key, out var originDefinition);
            targetByKey.TryGetValue(key, out var targetDefinition);
            if (originDefinition is not null
                && targetDefinition is not null
                && ConfigurationJsonSemanticComparer.Equals(originDefinition.Json, targetDefinition.Json))
            {
                continue;
            }

            changes.Add(new ConfigurationUnifiedVersionDefinitionChange
            {
                DefinitionKey = key,
                DisplayName = targetDefinition?.DisplayName ?? originDefinition?.DisplayName ?? key,
                OriginJson = originDefinition?.Json,
                TargetJson = targetDefinition?.Json,
                ChangeKind = originDefinition is null
                    ? ConfigurationUnifiedVersionDefinitionChangeKind.Added
                    : targetDefinition is null
                        ? ConfigurationUnifiedVersionDefinitionChangeKind.Removed
                        : ConfigurationUnifiedVersionDefinitionChangeKind.Modified
            });
        }

        return new ConfigurationUnifiedVersionComparison
        {
            Origin = origin,
            Target = target,
            Changes = changes
        };
    }

    public async Task<ConfigurationUnifiedVersionApplyPreview> PreviewRollbackAsync(
        long version,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetRequiredVersionAsync(version, cancellationToken);
        return await rollbackPreviewFactory.CreateAsync(snapshot, cancellationToken);
    }

    public async Task<ConfigurationUnifiedVersionRollbackResult> RollbackToVersionAsync(
        ConfigurationUnifiedVersionRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetRequiredVersionAsync(request.Version, cancellationToken);
        var preview = await rollbackPreviewFactory.CreateAsync(snapshot, cancellationToken);
        if (!string.Equals(preview.PlanToken, request.PlanToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The rollback preview is stale because configuration values, schemas, or destinations changed. Review a new preview before applying.");
        }

        if (!preview.CanApplyWithAcknowledgement(request.AcknowledgeCompatibleSchemaDrift))
        {
            throw new InvalidOperationException(BuildApplyBlockMessage(preview, request.AcknowledgeCompatibleSchemaDrift));
        }

        var commands = BuildCommands(request.Version, preview);
        var applyResult = await mutationGroupApplyService.ApplyAsync(new ConfigurationMutationGroupApplyRequest
        {
            Label = $"Apply configuration version v{request.Version}",
            Reason = request.Reason,
            Context = new ConfigurationMutationContext { Reason = request.Reason },
            Commands = commands,
            ExpectedEffectiveValues = preview.Targets.Select(static target =>
                new ConfigurationExpectedEffectiveValue
                {
                    DefinitionKey = target.DefinitionKey,
                    Json = target.TargetJson
                }).ToArray()
        }, cancellationToken);
        var results = applyResult.Outcomes
            .Where(static outcome => outcome is
                { Status: ConfigurationMutationOutcomeStatus.Applied, Result: not null })
            .Select(outcome => outcome.Result! with { PostCommitIssues = applyResult.PostCommitIssues })
            .ToArray();

        return new ConfigurationUnifiedVersionRollbackResult
        {
            Version = request.Version,
            MutationGroup = applyResult.MutationGroup,
            Results = results,
            ApplyResult = applyResult
        };
    }

    private static IReadOnlyList<ConfigurationMutationCommand> BuildCommands(
        long version,
        ConfigurationUnifiedVersionApplyPreview preview)
    {
        var commands = new List<ConfigurationMutationCommand>(preview.MutationCount);
        var requestIndex = 0;
        foreach (var target in preview.ChangedTargets)
        {
            foreach (var mutation in target.Mutations)
            {
                commands.Add(new ConfigurationMutationCommand
                {
                    RequestId = $"unified-version:{version}:{requestIndex++}",
                    DefinitionKey = target.DefinitionKey,
                    LogicalPath = LogicalPath.Parse(mutation.LogicalPath),
                    MutationKind = mutation.MutationKind,
                    Value = mutation.MutationKind == ConfigurationMutationKind.Set
                        ? ConfigurationStoredValue.FromJson(mutation.TargetJson
                            ?? throw new InvalidOperationException(
                                $"Rollback path '{mutation.LogicalPath}' has no target value."))
                        : ConfigurationStoredValue.Null,
                    ExpectedSchemaVersion = target.CurrentSchemaVersion
                        ?? throw new InvalidOperationException(
                            $"Definition '{target.DefinitionKey}' has no reviewed schema version."),
                    ExpectedSchemaHash = target.CurrentSchemaHash,
                    ExpectedSourceChainRevision = mutation.ExpectedSourceChainRevision,
                    Target = CreateMutationTarget(target.DefinitionKey, mutation)
                });
            }
        }

        return commands;
    }

    private async Task<ConfigurationUnifiedVersionSnapshot> GetRequiredVersionAsync(
        long version,
        CancellationToken cancellationToken)
    {
        return await versionStore.GetVersionAsync(version, cancellationToken)
               ?? throw new KeyNotFoundException($"Unified configuration version '{version}' was not found.");
    }

    private static ConfigurationMutationTarget CreateMutationTarget(
        string definitionKey,
        ConfigurationUnifiedVersionApplyMutation mutation)
    {
        return mutation.SourceKind switch
        {
            ConfigurationSourceKind.JsonFile when mutation.SourceKey is { Length: > 0 } sourceKey =>
                new ConfigurationExternalSourceMutationTarget
                {
                    SourceKey = sourceKey,
                    ExpectedRevision = mutation.ExpectedSourceRevision
                },
            ConfigurationSourceKind.MonicaEffectiveStore => new ConfigurationEffectiveStoreMutationTarget
            {
                ExpectedVersion = mutation.ExpectedValueVersion
            },
            _ => throw new InvalidOperationException(
                $"Definition '{definitionKey}' path '{mutation.LogicalPath}' does not have a supported rollback destination.")
        };
    }

    private static string BuildApplyBlockMessage(
        ConfigurationUnifiedVersionApplyPreview preview,
        bool acknowledgedCompatibleSchemaDrift)
    {
        if (!preview.HasChanges)
        {
            return $"Configuration version v{preview.Version} already matches the current effective values.";
        }

        var target = preview.ChangedTargets.First(candidate =>
            !candidate.CanApply(acknowledgedCompatibleSchemaDrift));
        return target.Status switch
        {
            ConfigurationUnifiedVersionApplyTargetStatus.CompatibleSchemaDrift =>
                $"Definition '{target.DisplayName}' requires explicit acknowledgement of compatible schema drift.",
            ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue when
                target.ValidationIssues.FirstOrDefault(static issue => issue.DetailsHidden) is not null =>
                $"Definition '{target.DisplayName}' is incompatible with the current schema. Sensitive validation details were withheld.",
            ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue when
                target.ValidationIssues.FirstOrDefault() is { } issue =>
                $"Definition '{target.DisplayName}' is incompatible with the current schema at '{issue.LogicalPath}': {issue.Message}",
            ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue =>
                $"Definition '{target.DisplayName}' is incompatible with the current schema.",
            ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition =>
                $"Definition '{target.DefinitionKey}' is not known by the current process.",
            ConfigurationUnifiedVersionApplyTargetStatus.RuntimeOutOfSync =>
                $"Definition '{target.DisplayName}' has physical source values that differ from the currently loaded runtime values. Reload or reconcile the runtime before rolling back.",
            ConfigurationUnifiedVersionApplyTargetStatus.ReadOnlyOverride when
                target.Mutations.FirstOrDefault(static mutation =>
                    mutation.Status == ConfigurationUnifiedVersionApplyMutationStatus.ReadOnlyOverride) is { } blocked =>
                $"Read-only source '{blocked.BlockingSourceDisplayName}' controls path '{DisplayBlockPath(target, blocked.LogicalPath)}' in '{target.DisplayName}'.",
            ConfigurationUnifiedVersionApplyTargetStatus.CompositeSourceConflict when
                target.Mutations.FirstOrDefault(static mutation =>
                    mutation.Status == ConfigurationUnifiedVersionApplyMutationStatus.CompositeSourceConflict) is { } composite =>
                $"Path '{DisplayBlockPath(target, composite.LogicalPath)}' in '{target.DisplayName}' is composed from multiple sources and cannot be replaced in one source without creating unreviewed overrides.",
            ConfigurationUnifiedVersionApplyTargetStatus.LowerPriorityFallback when
                target.Mutations.FirstOrDefault(static mutation =>
                    mutation.Status == ConfigurationUnifiedVersionApplyMutationStatus.LowerPriorityFallback) is { } fallback =>
                $"Removing path '{DisplayBlockPath(target, fallback.LogicalPath)}' in '{target.DisplayName}' would reveal a value from lower-priority source '{fallback.BlockingSourceDisplayName}'.",
            _ => $"Definition '{target.DisplayName}' does not have a supported writable rollback destination."
        };
    }

    private static string DisplayBlockPath(ConfigurationUnifiedVersionApplyTarget target, string logicalPath)
    {
        return string.Equals(target.CapturedSchemaHash, target.CurrentSchemaHash, StringComparison.Ordinal)
            ? logicalPath
            : "<hidden because schema metadata changed>";
    }
}
