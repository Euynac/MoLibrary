using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default rollback service that translates inverse mutations into one coordinated mutation group.
/// </summary>
internal sealed class ConfigurationRollbackService(
    IConfigurationHistoryService historyService,
    IConfigurationMutationGroupApplyService mutationGroupApplyService,
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationSourceInspector sourceInspector,
    IConfigurationJsonFileSourceWriter sourceWriter,
    IConfigurationMutationGroupService groupService,
    ILogger<ConfigurationRollbackService> logger)
    : IConfigurationRollbackService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> RollbackHistoryAsync(
        string historyId,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var history = await GetRequiredHistoryAsync(historyId, cancellationToken);
        var applyResult = await ApplyRowsAsync(
            [history],
            $"Rollback {history.ModifiedTime:yyyy-MM-dd HH:mm:ss}",
            context,
            cancellationToken);

        return GetAppliedResults(applyResult).SingleOrDefault()
               ?? throw new InvalidOperationException(DescribeFailedOutcomes(applyResult));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationResult>> RollbackHistoriesAsync(
        IReadOnlyList<string> historyIds,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        if (historyIds.Count == 0)
        {
            throw new InvalidOperationException("At least one configuration history row is required for batch rollback.");
        }

        var rows = new List<ConfigurationValueHistory>();
        foreach (var historyId in historyIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(await GetRequiredHistoryAsync(historyId, cancellationToken));
        }

        var orderedRows = rows
            .OrderByDescending(static row => row.ModifiedTime)
            .ThenByDescending(static row => row.Version)
            .ToArray();
        var applyResult = await ApplyRowsAsync(
            orderedRows,
            $"Rollback {orderedRows.Length} selected configuration changes",
            context,
            cancellationToken);

        var results = GetAppliedResults(applyResult);
        return results.Count > 0
            ? results
            : throw new InvalidOperationException(DescribeFailedOutcomes(applyResult));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationResult>> RollbackGroupAsync(
        string groupId,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var originalGroup = await groupService.GetAsync(groupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Configuration mutation group '{groupId}' was not found.");
        var rows = await groupService.GetGroupHistoryAsync(groupId, cancellationToken);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Configuration mutation group '{groupId}' has no history rows to roll back.");
        }

        var orderedRows = rows
            .OrderByDescending(static row => row.ModifiedTime)
            .ThenByDescending(static row => row.Version)
            .ToArray();
        var applyResult = await ApplyRowsAsync(
            orderedRows,
            $"Rollback {originalGroup.Label}",
            context,
            cancellationToken);

        if (applyResult.Status == ConfigurationMutationGroupApplyStatus.Applied)
        {
            applyResult = await TryMarkOriginalGroupRolledBackAsync(groupId, applyResult, cancellationToken);
        }

        var results = GetAppliedResults(applyResult);
        return results.Count > 0
            ? results
            : throw new InvalidOperationException(DescribeFailedOutcomes(applyResult));
    }

    private async Task<ConfigurationMutationGroupApplyResult> ApplyRowsAsync(
        IReadOnlyList<ConfigurationValueHistory> rows,
        string label,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var commands = await BuildCommandsAsync(rows, cancellationToken);
        return await mutationGroupApplyService.ApplyAsync(new ConfigurationMutationGroupApplyRequest
        {
            Label = label,
            Reason = context.Reason,
            Context = context with { MutationGroupId = null },
            Commands = commands
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<ConfigurationMutationCommand>> BuildCommandsAsync(
        IReadOnlyList<ConfigurationValueHistory> rows,
        CancellationToken cancellationToken)
    {
        var effectiveVersions = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        var sourceRevisions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var commands = new List<ConfigurationMutationCommand>(rows.Count);

        foreach (var history in rows)
        {
            ConfigurationMutationTarget target;
            if (history.TargetKind == ConfigurationMutationTargetKind.ExternalConfigurationSource)
            {
                var sourceKey = ResolveSourceKey(history);
                if (!sourceRevisions.TryGetValue(sourceKey, out var revision))
                {
                    var source = sourceInspector.GetRequiredSource(sourceKey);
                    revision = await sourceWriter.GetRevisionAsync(source, cancellationToken);
                    sourceRevisions[sourceKey] = revision;
                }

                target = new ConfigurationExternalSourceMutationTarget
                {
                    SourceKey = sourceKey,
                    ExpectedRevision = revision
                };
            }
            else
            {
                if (!effectiveVersions.TryGetValue(history.DefinitionKey, out var version))
                {
                    version = (await effectiveValueStore.GetAsync(history.DefinitionKey, cancellationToken))?.Version;
                    effectiveVersions[history.DefinitionKey] = version;
                }

                target = new ConfigurationEffectiveStoreMutationTarget { ExpectedVersion = version };
            }

            commands.Add(new ConfigurationMutationCommand
            {
                RequestId = $"rollback:{history.HistoryId}",
                DefinitionKey = history.DefinitionKey,
                LogicalPath = history.LogicalPath,
                MutationKind = history.OldValue is null
                    ? ConfigurationMutationKind.Remove
                    : ConfigurationMutationKind.Set,
                Value = history.OldValue ?? ConfigurationStoredValue.Null,
                ExpectedSchemaVersion = history.SchemaVersion,
                Target = target
            });
        }

        return commands;
    }

    private async Task<ConfigurationMutationGroupApplyResult> TryMarkOriginalGroupRolledBackAsync(
        string originalGroupId,
        ConfigurationMutationGroupApplyResult applyResult,
        CancellationToken cancellationToken)
    {
        try
        {
            await groupService.MarkRolledBackAsync(
                originalGroupId,
                applyResult.MutationGroup.GroupId,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return applyResult;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Configuration group {OriginalGroupId} was rolled back by {RollbackGroupId}, but the original group audit marker could not be updated.",
                originalGroupId,
                applyResult.MutationGroup.GroupId);
            return applyResult with
            {
                PostCommitIssues =
                [
                    .. applyResult.PostCommitIssues,
                    new ConfigurationPostCommitIssue
                    {
                        Kind = ConfigurationPostCommitIssueKind.AuditFinalization,
                        Source = groupService.GetType().Name,
                        Message = "The rollback was applied, but the original mutation-group audit marker could not be updated.",
                        Detail = ex.ToString()
                    }
                ]
            };
        }
    }

    private async Task<ConfigurationValueHistory> GetRequiredHistoryAsync(
        string historyId,
        CancellationToken cancellationToken)
    {
        return await historyService.GetHistoryByIdAsync(historyId, cancellationToken)
               ?? throw new KeyNotFoundException($"Configuration history row '{historyId}' was not found.");
    }

    private static IReadOnlyList<ConfigurationMutationResult> GetAppliedResults(
        ConfigurationMutationGroupApplyResult applyResult)
    {
        return applyResult.Outcomes
            .Where(static outcome => outcome is
                { Status: ConfigurationMutationOutcomeStatus.Applied, Result: not null })
            .Select(outcome => outcome.Result! with { PostCommitIssues = applyResult.PostCommitIssues })
            .ToArray();
    }

    private static string DescribeFailedOutcomes(ConfigurationMutationGroupApplyResult applyResult)
    {
        var diagnostics = applyResult.Outcomes
            .Where(static outcome => outcome.Status != ConfigurationMutationOutcomeStatus.Applied)
            .Select(static outcome => $"{outcome.RequestId}: {outcome.ErrorMessage ?? outcome.Status.ToString()}");
        return $"No rollback mutation was applied. {string.Join("; ", diagnostics)}";
    }

    private string ResolveSourceKey(ConfigurationValueHistory history)
    {
        if (string.IsNullOrWhiteSpace(history.SourcePhysicalPath))
        {
            throw new InvalidOperationException($"Configuration history row '{history.HistoryId}' does not contain a physical source path.");
        }

        var source = sourceInspector.GetSources().FirstOrDefault(candidate =>
            candidate.Kind == ConfigurationSourceKind.JsonFile
            && !string.IsNullOrWhiteSpace(candidate.PhysicalPath)
            && string.Equals(
                Path.GetFullPath(candidate.PhysicalPath),
                Path.GetFullPath(history.SourcePhysicalPath),
                StringComparison.OrdinalIgnoreCase));

        return source?.SourceKey
               ?? throw new InvalidOperationException(
                   $"Configuration source '{history.SourcePhysicalPath}' is no longer registered and cannot be rolled back.");
    }
}
