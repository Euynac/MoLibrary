using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default rollback service that replays inverse mutations through the standard mutation pipeline.
/// </summary>
internal sealed class ConfigurationRollbackService(
    IConfigurationHistoryService historyService,
    IConfigurationMutationService mutationService,
    IConfigurationSourceMutationService sourceMutationService,
    IConfigurationSourceInspector sourceInspector,
    IConfigurationMutationGroupService groupService)
    : IConfigurationRollbackService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> RollbackHistoryAsync(
        string historyId,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var history = await historyService.GetHistoryByIdAsync(historyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Configuration history row '{historyId}' was not found.");

        var group = await groupService.BeginAsync(
            $"Rollback {history.ModifiedTime:yyyy-MM-dd HH:mm:ss}",
            context.Reason,
            context,
            cancellationToken);

        try
        {
            var result = await RollbackHistoryRowAsync(history, context with { MutationGroupId = group.GroupId }, cancellationToken);
            await groupService.CompleteAsync(group.GroupId, 1, [history.DefinitionKey], cancellationToken);
            return result;
        }
        catch
        {
            await groupService.MarkPartialAsync(group.GroupId, 0, [], cancellationToken);
            throw;
        }
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
            rows.Add(await historyService.GetHistoryByIdAsync(historyId, cancellationToken)
                     ?? throw new KeyNotFoundException($"Configuration history row '{historyId}' was not found."));
        }

        var orderedRows = rows
            .OrderByDescending(row => row.ModifiedTime)
            .ThenByDescending(row => row.Version)
            .ToArray();
        var group = await groupService.BeginAsync(
            $"Rollback {orderedRows.Length} selected configuration changes",
            context.Reason,
            context,
            cancellationToken);
        var rollbackContext = context with { MutationGroupId = group.GroupId };
        var results = new List<ConfigurationMutationResult>();
        var definitionKeys = new List<string>();

        try
        {
            foreach (var history in orderedRows)
            {
                results.Add(await RollbackHistoryRowAsync(history, rollbackContext, cancellationToken));
                definitionKeys.Add(history.DefinitionKey);
            }

            await groupService.CompleteAsync(group.GroupId, results.Count, definitionKeys, cancellationToken);
            return results;
        }
        catch
        {
            await groupService.MarkPartialAsync(group.GroupId, results.Count, definitionKeys, cancellationToken);
            throw;
        }
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

        var rollbackGroup = await groupService.BeginAsync(
            $"Rollback {originalGroup.Label}",
            context.Reason,
            context,
            cancellationToken);
        var rollbackContext = context with { MutationGroupId = rollbackGroup.GroupId };
        var results = new List<ConfigurationMutationResult>();
        var definitionKeys = new List<string>();

        try
        {
            foreach (var history in rows.OrderByDescending(row => row.ModifiedTime).ThenByDescending(row => row.Version))
            {
                results.Add(await RollbackHistoryRowAsync(history, rollbackContext, cancellationToken));
                definitionKeys.Add(history.DefinitionKey);
            }

            await groupService.CompleteAsync(rollbackGroup.GroupId, results.Count, definitionKeys, cancellationToken);
            await groupService.MarkRolledBackAsync(groupId, rollbackGroup.GroupId, DateTimeOffset.UtcNow, cancellationToken);
            return results;
        }
        catch
        {
            await groupService.MarkPartialAsync(rollbackGroup.GroupId, results.Count, definitionKeys, cancellationToken);
            throw;
        }
    }

    private Task<ConfigurationMutationResult> RollbackHistoryRowAsync(
        ConfigurationValueHistory history,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var request = history.OldValue is null
            ? BuildRemoveRequest(history, context)
            : BuildRestoreRequest(history, context);

        if (history.TargetKind == ConfigurationMutationTargetKind.ExternalConfigurationSource)
        {
            var sourceRequest = BuildSourceRequest(history, request, ResolveSourceKey(history));
            return sourceMutationService.MutateAsync(sourceRequest, cancellationToken);
        }

        return mutationService.MutateAsync(request, cancellationToken);
    }

    private static ConfigurationMutationRequest BuildRemoveRequest(
        ConfigurationValueHistory history,
        ConfigurationMutationContext context)
    {
        return new ConfigurationMutationRequest
        {
            DefinitionKey = history.DefinitionKey,
            LogicalPath = history.LogicalPath,
            MutationKind = ConfigurationMutationKind.Remove,
            Value = ConfigurationStoredValue.Null,
            ExpectedSchemaVersion = history.SchemaVersion,
            Context = context
        };
    }

    private static ConfigurationMutationRequest BuildRestoreRequest(
        ConfigurationValueHistory history,
        ConfigurationMutationContext context)
    {
        return new ConfigurationMutationRequest
        {
            DefinitionKey = history.DefinitionKey,
            LogicalPath = history.LogicalPath,
            MutationKind = ConfigurationMutationKind.Set,
            Value = history.OldValue!,
            ExpectedSchemaVersion = history.SchemaVersion,
            Context = context
        };
    }

    private static ConfigurationSourceMutationRequest BuildSourceRequest(
        ConfigurationValueHistory history,
        ConfigurationMutationRequest request,
        string sourceKey)
    {
        return new ConfigurationSourceMutationRequest
        {
            SourceKey = sourceKey,
            DefinitionKey = request.DefinitionKey,
            LogicalPath = request.LogicalPath,
            MutationKind = request.MutationKind,
            Value = request.Value,
            ExpectedSchemaVersion = request.ExpectedSchemaVersion,
            ExpectedSourceRevision = history.SourceRevisionAfter,
            Context = request.Context
        };
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
