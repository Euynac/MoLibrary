using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.Providers;

public sealed partial class InMemoryJobSchedulerStore
{
    /// <inheritdoc />
    public async Task<JobOperationalSummary?> GetOperationalSummaryAsync(
        string schedulerScopeKey,
        JobId jobId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        jobId.Validate();
        var page = await QueryOperationalSummariesAsync(
            schedulerScopeKey,
            new JobDefinitionQuery
            {
                OwnerKey = jobId.OwnerKey,
                JobKey = jobId.JobKey,
                PageNumber = 1,
                PageSize = 1
            },
            cancellationToken);
        return page.Items.SingleOrDefault();
    }

    /// <inheritdoc />
    public Task<QueryResult<JobOperationalSummary>> QueryOperationalSummariesAsync(
        string schedulerScopeKey,
        JobDefinitionQuery query,
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
            var filtered = FilterDefinitions(GetScopeDefinitions(schedulerScopeKey), query);
            var ordered = filtered.ApplyDefinitionOrdering(query).ToArray();
            var definitions = ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            if (definitions.Length == 0)
            {
                return Task.FromResult(new QueryResult<JobOperationalSummary>([], ordered.Length));
            }

            var definitionKeys = definitions
                .Select(DefinitionKeyOf)
                .ToHashSet();
            var executions = _executionInstances.Values
                .Where(execution => definitionKeys.Contains(GetExecutionGateKey(execution.Template)))
                .ToArray();
            var latestExecutions = executions
                .GroupBy(static execution => new JobId(execution.Template.OwnerKey, execution.Template.JobKey))
                .ToDictionary(
                    static group => group.Key,
                    static group => ToSummary(group
                        .OrderByDescending(static execution => execution.CreatedAtUtc)
                        .ThenByDescending(static execution => execution.InstanceId, StringComparer.Ordinal)
                        .First()));
            var activeCounts = executions
                .Where(static execution => execution.State is JobExecutionState.Queued or JobExecutionState.Running)
                .GroupBy(static execution => (execution.Template.OwnerKey, execution.Template.JobKey, execution.State))
                .ToDictionary(static group => group.Key, static group => group.Count());

            var summaries = definitions.Select(definition =>
            {
                var jobId = definition.Id;
                _recurringCursors.TryGetValue(DefinitionKeyOf(definition), out var cursor);
                var suspensionReasons = GetRecurringSuspensionReasons(definition, cursor);
                var recurringStatus = GetRecurringStatus(definition, cursor, suspensionReasons);
                return new JobOperationalSummary
                {
                    Definition = definition,
                    RecurringScheduleStatus = recurringStatus,
                    SuspensionReasons = suspensionReasons,
                    NextOccurrenceUtc = recurringStatus == JobRecurringScheduleStatus.Scheduled
                        ? cursor!.NextOccurrenceUtc
                        : null,
                    LatestExecution = latestExecutions.GetValueOrDefault(jobId),
                    QueuedExecutionCount = activeCounts.GetValueOrDefault(
                        (jobId.OwnerKey, jobId.JobKey, JobExecutionState.Queued)),
                    RunningExecutionCount = activeCounts.GetValueOrDefault(
                        (jobId.OwnerKey, jobId.JobKey, JobExecutionState.Running))
                };
            }).ToList();

            return Task.FromResult(new QueryResult<JobOperationalSummary>(summaries, ordered.Length));
        }
    }

    private static JobRecurringScheduleStatus GetRecurringStatus(
        JobDefinition definition,
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
        JobDefinition definition,
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
