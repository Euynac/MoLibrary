using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.EfCore;

public sealed partial class EfCoreJobSchedulerStore
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

        return ReadAsync(async (dbContext, token) =>
        {
            var filtered = FilterDefinitions(dbContext, schedulerScopeKey, query);
            var totalCount = await filtered.CountAsync(token);
            var ordered = OrderDefinitions(filtered, query);
            var entities = await ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(token);
            var definitions = entities.Select(ToDefinition).ToArray();
            if (definitions.Length == 0)
            {
                return new QueryResult<JobOperationalSummary>([], totalCount);
            }

            // Match the exact (OwnerKey, JobKey) pairs on the page; independent owner/job key filters would also scan
            // every crossed combination of the two sets.
            var identities = definitions.Select(static definition => definition.Id).ToArray();
            var executionQuery = dbContext.Executions.AsNoTracking()
                .Where(execution => execution.SchedulerScopeKey == schedulerScopeKey)
                .Where(CreateIdentityPredicate<JobExecutionEntity>(identities));
            var latestExecutionEntities = await executionQuery
                .Where(candidate => candidate.InstanceId == executionQuery
                    .Where(execution => execution.OwnerKey == candidate.OwnerKey
                                        && execution.JobKey == candidate.JobKey)
                    .OrderByDescending(execution => execution.CreatedAtUtcTicks)
                    .ThenByDescending(execution => execution.InstanceId)
                    .Select(execution => execution.InstanceId)
                    .First())
                .ToArrayAsync(token);
            var latestExecutions = latestExecutionEntities.ToDictionary(
                static execution => new JobId(execution.OwnerKey, execution.JobKey),
                ToExecution);
            var activeCountRows = await executionQuery
                .Where(execution =>
                    execution.State == JobExecutionState.Queued
                    || execution.State == JobExecutionState.Running)
                .GroupBy(execution => new { execution.OwnerKey, execution.JobKey, execution.State })
                .Select(group => new
                {
                    group.Key.OwnerKey,
                    group.Key.JobKey,
                    group.Key.State,
                    Count = group.Count()
                })
                .ToArrayAsync(token);
            var activeCounts = activeCountRows.ToDictionary(
                static row => (new JobId(row.OwnerKey, row.JobKey), row.State),
                static row => row.Count);

            var cursorEntities = await dbContext.RecurringCursors.AsNoTracking()
                .Where(cursor => cursor.SchedulerScopeKey == schedulerScopeKey)
                .Where(CreateIdentityPredicate<JobRecurringCursorEntity>(identities))
                .ToArrayAsync(token);
            var cursors = cursorEntities.ToDictionary(
                static cursor => new JobId(cursor.OwnerKey, cursor.JobKey),
                ToCursor);

            var summaries = definitions.Select(definition =>
            {
                var jobId = definition.Id;
                cursors.TryGetValue(jobId, out var cursor);
                var suspensionReasons = GetRecurringSuspensionReasons(definition, cursor);
                var recurringStatus = GetOperationalRecurringStatus(definition, cursor, suspensionReasons);
                return new JobOperationalSummary
                {
                    Definition = definition,
                    RecurringScheduleStatus = recurringStatus,
                    SuspensionReasons = suspensionReasons,
                    NextOccurrenceUtc = recurringStatus == JobRecurringScheduleStatus.Scheduled
                        ? cursor!.NextOccurrenceUtc
                        : null,
                    LatestExecution = latestExecutions.GetValueOrDefault(jobId),
                    QueuedExecutionCount = activeCounts.GetValueOrDefault((jobId, JobExecutionState.Queued)),
                    RunningExecutionCount = activeCounts.GetValueOrDefault((jobId, JobExecutionState.Running))
                };
            }).ToList();

            return new QueryResult<JobOperationalSummary>(summaries, totalCount);
        }, cancellationToken);
    }

    private static JobRecurringScheduleSuspensionReason GetRecurringSuspensionReasons(
        JobDefinition definition,
        RecurringScheduleCursor? cursor)
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

    private static JobRecurringScheduleStatus GetOperationalRecurringStatus(
        JobDefinition definition,
        RecurringScheduleCursor? cursor,
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
}
