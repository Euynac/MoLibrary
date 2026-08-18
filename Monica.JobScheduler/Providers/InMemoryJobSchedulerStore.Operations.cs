using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.Providers;

public sealed partial class InMemoryJobSchedulerStore
{
    /// <inheritdoc />
    public async Task<JobOperationalSummary?> GetOperationalSummaryAsync(
        string schedulerScopeKey,
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        var page = await QueryOperationalSummariesAsync(
            schedulerScopeKey,
            new JobCatalogQuery { JobKey = jobKey, PageNumber = 1, PageSize = 1 },
            cancellationToken);
        return page.Items.SingleOrDefault();
    }

    /// <inheritdoc />
    public Task<QueryResult<JobOperationalSummary>> QueryOperationalSummariesAsync(
        string schedulerScopeKey,
        JobCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageNumber < 1 || query.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page number and size must be positive.");
        }
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
                || scope.ActiveReleaseId is null)
            {
                return Task.FromResult(new QueryResult<JobOperationalSummary>([], 0));
            }

            var filtered = FilterDefinitions(ProjectActiveDefinitions(schedulerScopeKey, scope), query);

            var ordered = filtered.ApplyCatalogOrdering(query).ToArray();
            var definitions = ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            if (definitions.Length == 0)
            {
                return Task.FromResult(new QueryResult<JobOperationalSummary>([], ordered.Length));
            }

            var jobKeys = definitions
                .Select(static definition => definition.Declaration.JobKey)
                .ToHashSet(StringComparer.Ordinal);
            var executions = _executionInstances.Values
                .Where(execution =>
                    string.Equals(
                        execution.Template.Revision.SchedulerScopeKey,
                        schedulerScopeKey,
                        StringComparison.Ordinal)
                    && jobKeys.Contains(execution.Template.Revision.JobKey))
                .ToArray();
            var latestExecutions = executions
                .GroupBy(static execution => execution.Template.Revision.JobKey, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => ToSummary(group
                        .OrderByDescending(static execution => execution.CreatedAtUtc)
                        .ThenByDescending(static execution => execution.InstanceId, StringComparer.Ordinal)
                        .First()),
                    StringComparer.Ordinal);
            var activeCounts = executions
                .Where(static execution => execution.State is JobExecutionState.Queued or JobExecutionState.Running)
                .GroupBy(static execution => (execution.Template.Revision.JobKey, execution.State))
                .ToDictionary(static group => group.Key, static group => group.Count());
            var cursorKeys = definitions.Select(definition => new RecurringCursorStorageKey(
                schedulerScopeKey,
                definition.ActivationEpoch,
                definition.JobRevisionId)).ToHashSet();
            var cursors = _recurringCursors
                .Where(entry => cursorKeys.Contains(entry.Key))
                .Select(static entry => entry.Value)
                .ToDictionary(static cursor => cursor.Template.Revision.JobRevisionId, StringComparer.Ordinal);
            var now = UtcNow;
            var capabilities = _workerCapabilityLeases.Values
                .Where(capability =>
                    string.Equals(capability.SchedulerScopeKey, schedulerScopeKey, StringComparison.Ordinal)
                    && capability.LeaseExpiresAtUtc > now)
                .ToArray();

            var summaries = definitions.Select(definition =>
            {
                cursors.TryGetValue(definition.JobRevisionId, out var cursor);
                var suspensionReasons = GetRecurringSuspensionReasons(definition, cursor);
                var recurringStatus = GetRecurringStatus(definition, cursor, suspensionReasons);
                var jobKey = definition.Declaration.JobKey;
                return new JobOperationalSummary
                {
                    Definition = definition,
                    RecurringScheduleStatus = recurringStatus,
                    SuspensionReasons = suspensionReasons,
                    NextOccurrenceUtc = recurringStatus == JobRecurringScheduleStatus.Scheduled
                        ? cursor!.NextOccurrenceUtc
                        : null,
                    LatestExecution = latestExecutions.GetValueOrDefault(jobKey),
                    QueuedExecutionCount = activeCounts.GetValueOrDefault((jobKey, JobExecutionState.Queued)),
                    RunningExecutionCount = activeCounts.GetValueOrDefault((jobKey, JobExecutionState.Running)),
                    CompatibleWorkerCount = capabilities.Count(capability =>
                        string.Equals(capability.OwnerKey, definition.OwnerId, StringComparison.Ordinal)
                        && string.Equals(
                            capability.WorkerRevisionId,
                            definition.WorkerRevisionId,
                            StringComparison.Ordinal)
                        && capability.JobRevisionIds.Contains(definition.JobRevisionId))
                };
            }).ToList();

            return Task.FromResult(new QueryResult<JobOperationalSummary>(summaries, ordered.Length));
        }
    }

    private static IEnumerable<ActiveJobDefinition> FilterDefinitions(
        IEnumerable<ActiveJobDefinition> definitions,
        JobCatalogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.JobKey))
        {
            definitions = definitions.Where(item =>
                string.Equals(item.Declaration.JobKey, query.JobKey, StringComparison.Ordinal));
        }
        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            definitions = definitions.Where(item =>
                string.Equals(item.OwnerId, query.OwnerId, StringComparison.Ordinal));
        }
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            definitions = definitions.Where(item =>
                item.Declaration.JobKey.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || item.EffectiveConfiguration.JobName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || item.EffectiveConfiguration.Description?.Contains(
                    query.SearchText,
                    StringComparison.OrdinalIgnoreCase) == true);
        }
        if (query.JobType is { } jobType)
        {
            definitions = definitions.Where(item => item.Declaration.JobType == jobType);
        }
        if (query.IsDisabled is { } isDisabled)
        {
            definitions = definitions.Where(item => item.IsDisabled == isDisabled);
        }

        return definitions;
    }

    private static JobRecurringScheduleStatus GetRecurringStatus(
        ActiveJobDefinition definition,
        StoredRecurringCursor? cursor,
        JobRecurringScheduleSuspensionReason suspensionReasons)
    {
        if (definition.Declaration.JobType != JobType.Recurring)
        {
            return JobRecurringScheduleStatus.NotRecurring;
        }
        if (suspensionReasons != JobRecurringScheduleSuspensionReason.None)
        {
            return JobRecurringScheduleStatus.Suspended;
        }
        if (cursor is null)
        {
            return JobRecurringScheduleStatus.AwaitingSynchronization;
        }

        return cursor.NextOccurrenceUtc is null
            ? JobRecurringScheduleStatus.Exhausted
            : JobRecurringScheduleStatus.Scheduled;
    }

    private static JobRecurringScheduleSuspensionReason GetRecurringSuspensionReasons(
        ActiveJobDefinition definition,
        StoredRecurringCursor? cursor)
    {
        if (definition.Declaration.JobType != JobType.Recurring)
        {
            return JobRecurringScheduleSuspensionReason.None;
        }

        var reasons = cursor?.SuspensionReasons ?? JobRecurringScheduleSuspensionReason.None;
        return definition.IsDisabled
            ? reasons | JobRecurringScheduleSuspensionReason.OperatorPolicy
            : reasons;
    }
}
