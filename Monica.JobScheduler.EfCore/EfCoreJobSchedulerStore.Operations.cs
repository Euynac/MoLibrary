using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.EfCore;

public sealed partial class EfCoreJobSchedulerStore
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

        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            if (scope is null || scope.ActiveReleaseId is null)
            {
                return new QueryResult<JobOperationalSummary>([], 0);
            }

            var filtered = FilterDefinitions(
                await ProjectActiveDefinitionsAsync(dbContext, scope, token),
                query);

            var ordered = filtered.OrderBy(item => item.Declaration.JobKey, StringComparer.Ordinal).ToArray();
            var definitions = ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            if (definitions.Length == 0)
            {
                return new QueryResult<JobOperationalSummary>([], ordered.Length);
            }

            var jobKeys = definitions.Select(static definition => definition.Declaration.JobKey).ToArray();
            var executionQuery = dbContext.Executions.AsNoTracking().Where(execution =>
                execution.SchedulerScopeKey == schedulerScopeKey
                && jobKeys.Contains(execution.JobKey));
            var latestExecutionEntities = await executionQuery
                .Where(candidate => candidate.InstanceId == executionQuery
                    .Where(execution => execution.JobKey == candidate.JobKey)
                    .OrderByDescending(execution => execution.CreatedAtUtcTicks)
                    .ThenByDescending(execution => execution.InstanceId)
                    .Select(execution => execution.InstanceId)
                    .First())
                .ToArrayAsync(token);
            var latestExecutions = latestExecutionEntities.ToDictionary(
                static execution => execution.JobKey,
                ToExecution,
                StringComparer.Ordinal);
            var activeCountRows = await executionQuery
                .Where(execution =>
                    execution.State == JobExecutionState.Queued
                    || execution.State == JobExecutionState.Running)
                .GroupBy(execution => new { execution.JobKey, execution.State })
                .Select(group => new
                {
                    group.Key.JobKey,
                    group.Key.State,
                    Count = group.Count()
                })
                .ToArrayAsync(token);
            var activeCounts = activeCountRows.ToDictionary(
                static row => (row.JobKey, row.State),
                static row => row.Count);

            var jobRevisionIds = definitions.Select(static definition => definition.JobRevisionId).ToArray();
            var cursorEntities = await dbContext.RecurringCursors.AsNoTracking()
                .Where(cursor =>
                    cursor.SchedulerScopeKey == schedulerScopeKey
                    && cursor.ActivationEpoch == scope.ActivationEpoch
                    && jobRevisionIds.Contains(cursor.JobRevisionId))
                .ToArrayAsync(token);
            var cursors = cursorEntities.ToDictionary(
                static cursor => cursor.JobRevisionId,
                ToRecurringCursor,
                StringComparer.Ordinal);

            var nowTicks = ToTicks(await GetUtcNowAsync(dbContext, token));
            var ownerIds = definitions.Select(static definition => definition.OwnerId).Distinct().ToArray();
            var workerRevisionIds = definitions
                .Select(static definition => definition.WorkerRevisionId)
                .Distinct()
                .ToArray();
            var capabilityEntities = await dbContext.WorkerCapabilities.AsNoTracking()
                .Where(capability =>
                    capability.SchedulerScopeKey == schedulerScopeKey
                    && capability.LeaseExpiresAtUtcTicks > nowTicks
                    && ownerIds.Contains(capability.OwnerKey)
                    && workerRevisionIds.Contains(capability.WorkerRevisionId))
                .ToArrayAsync(token);
            var capabilities = capabilityEntities.Select(ToCapability).ToArray();

            var summaries = definitions.Select(definition =>
            {
                cursors.TryGetValue(definition.JobRevisionId, out var cursor);
                var recurringStatus = GetOperationalRecurringStatus(definition, cursor);
                var jobKey = definition.Declaration.JobKey;
                return new JobOperationalSummary
                {
                    Definition = definition,
                    RecurringScheduleStatus = recurringStatus,
                    NextOccurrenceUtc = recurringStatus == JobRecurringScheduleStatus.Scheduled
                        ? cursor!.NextOccurrenceUtc
                        : null,
                    LatestExecution = latestExecutions.GetValueOrDefault(jobKey),
                    QueuedExecutionCount = activeCounts.GetValueOrDefault((jobKey, JobExecutionState.Queued)),
                    RunningExecutionCount = activeCounts.GetValueOrDefault((jobKey, JobExecutionState.Running)),
                    CompatibleWorkerCount = capabilities.Count(capability =>
                        string.Equals(
                            capability.Capability.OwnerKey,
                            definition.OwnerId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            capability.Capability.WorkerRevisionId,
                            definition.WorkerRevisionId,
                            StringComparison.Ordinal)
                        && capability.Capability.JobRevisionIds.Contains(
                            definition.JobRevisionId,
                            StringComparer.Ordinal))
                };
            }).ToList();

            return new QueryResult<JobOperationalSummary>(summaries, ordered.Length);
        }, cancellationToken);
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
                || item.Declaration.JobName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
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

    private static JobRecurringScheduleStatus GetOperationalRecurringStatus(
        ActiveJobDefinition definition,
        RecurringScheduleCursor? cursor)
    {
        if (definition.Declaration.JobType != JobType.Recurring)
        {
            return JobRecurringScheduleStatus.NotRecurring;
        }
        if (definition.IsDisabled || cursor?.IsSuspended is true)
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
