using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.EfCore;

public sealed partial class EfCoreJobSchedulerStore
{
    /// <inheritdoc />
    public Task<RecurringScheduleSynchronizationResult> SynchronizeRecurringScheduleAsync(
        RecurringScheduleSynchronization synchronization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        synchronization.Validate();
        return WriteAsync(async (dbContext, token) =>
        {
            var revision = synchronization.Template.Revision;
            var cursor = await LoadRecurringCursorAsync(dbContext, new RecurringScheduleCursorKey
            {
                SchedulerScopeKey = revision.SchedulerScopeKey,
                ActivationEpoch = revision.ActivationEpoch,
                JobRevisionId = revision.JobRevisionId
            }, true, token);
            var scope = await dbContext.CatalogScopes.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == revision.SchedulerScopeKey,
                token);
            if (scope?.ActiveReleaseId is null
                || scope.ActiveIntentEpoch != scope.DesiredIntentEpoch
                || scope.ActivationEpoch != revision.ActivationEpoch)
            {
                return new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.InactiveActivation,
                    Cursor = cursor is null ? null : ToRecurringCursor(cursor)
                };
            }

            if (synchronization.ChangeEpoch < scope.ChangeEpoch
                || cursor is not null && synchronization.ChangeEpoch < cursor.LastSynchronizedChangeEpoch)
            {
                return new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.StaleChangeEpoch,
                    Cursor = cursor is null ? null : ToRecurringCursor(cursor)
                };
            }
            if (synchronization.ChangeEpoch > scope.ChangeEpoch)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(synchronization),
                    synchronization.ChangeEpoch,
                    $"Catalog change epoch {synchronization.ChangeEpoch} has not been committed.");
            }

            var policyDisabled = await IsActiveJobDisabledAsync(dbContext, revision, token);
            var isSuspended = synchronization.IsSuspended || policyDisabled;
            if (cursor is not null)
            {
                if (Deserialize<JobExecutionTemplate>(cursor.TemplateJson) != synchronization.Template
                    || Deserialize<RecurringScheduleDefinition>(cursor.ScheduleJson) != synchronization.Schedule)
                {
                    throw new InvalidOperationException(
                        $"Recurring cursor '{revision.JobRevisionId}' was synchronized with a different execution template.");
                }
                if (cursor.LastSynchronizedChangeEpoch == synchronization.ChangeEpoch)
                {
                    return new RecurringScheduleSynchronizationResult
                    {
                        Status = RecurringScheduleSynchronizationStatus.Unchanged,
                        Cursor = ToRecurringCursor(cursor)
                    };
                }

                var now = await GetUtcNowAsync(dbContext, token);
                if (cursor.IsSuspended != isSuspended)
                {
                    cursor.IsSuspended = isSuspended;
                    cursor.NextOccurrenceUtcTicks = isSuspended
                        ? null
                        : ToTicks(synchronization.Schedule.GetNextOccurrence(now));
                }
                cursor.LastSynchronizedChangeEpoch = synchronization.ChangeEpoch;
                cursor.CursorVersion++;
                cursor.UpdatedAtUtcTicks = ToTicks(now);
                cursor.ConcurrencyToken = NewVersion();
                return new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.Updated,
                    Cursor = ToRecurringCursor(cursor)
                };
            }

            var createdAtUtc = await GetUtcNowAsync(dbContext, token);
            // Activation, rather than this replica's first synchronization time, is the durable admission boundary.
            // A later resume follows the separate branch above and intentionally starts from database current time.
            var activationBoundaryUtc = FromTicks(await dbContext.CatalogActivations
                .AsNoTracking()
                .Where(item => item.SchedulerScopeKey == revision.SchedulerScopeKey
                               && item.ActivationEpoch == revision.ActivationEpoch)
                .Select(static item => item.ActivatedAtUtcTicks)
                .SingleAsync(token));
            cursor = new JobRecurringCursorEntity
            {
                SchedulerScopeKey = revision.SchedulerScopeKey,
                ActivationEpoch = revision.ActivationEpoch,
                JobRevisionId = revision.JobRevisionId,
                TemplateJson = Serialize(synchronization.Template),
                ScheduleJson = Serialize(synchronization.Schedule),
                NextOccurrenceUtcTicks = isSuspended
                    ? null
                    : ToTicks(synchronization.Schedule.GetNextOccurrence(activationBoundaryUtc)),
                LastSynchronizedChangeEpoch = synchronization.ChangeEpoch,
                IsSuspended = isSuspended,
                CursorVersion = 1,
                UpdatedAtUtcTicks = ToTicks(createdAtUtc),
                ConcurrencyToken = NewVersion()
            };
            dbContext.RecurringCursors.Add(cursor);
            return new RecurringScheduleSynchronizationResult
            {
                Status = RecurringScheduleSynchronizationStatus.Created,
                Cursor = ToRecurringCursor(cursor)
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RecurringScheduleCursor>> GetDueRecurringSchedulesAsync(
        string schedulerScopeKey,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Result count must be greater than zero.");
        }

        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            if (scope?.ActiveReleaseId is null || scope.ActiveIntentEpoch != scope.DesiredIntentEpoch)
            {
                return (IReadOnlyList<RecurringScheduleCursor>)[];
            }

            var nowTicks = ToTicks(await GetUtcNowAsync(dbContext, token));
            var entities = await dbContext.RecurringCursors.AsNoTracking()
                .Where(item => item.SchedulerScopeKey == schedulerScopeKey
                               && item.ActivationEpoch == scope.ActivationEpoch
                               && !item.IsSuspended
                               && item.NextOccurrenceUtcTicks <= nowTicks)
                .OrderBy(item => item.NextOccurrenceUtcTicks)
                .ThenBy(item => item.JobRevisionId)
                .Take(maxCount)
                .ToArrayAsync(token);
            return (IReadOnlyList<RecurringScheduleCursor>)entities.Select(ToRecurringCursor).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RecurringMaterializationResult> TryMaterializeRecurringOccurrenceAsync(
        RecurringOccurrenceMaterialization materialization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(materialization);
        materialization.Validate();
        return WriteAsync(async (dbContext, token) =>
        {
            var expectedTicks = ToTicks(materialization.ExpectedOccurrenceUtc);
            var cursor = await LoadRecurringCursorAsync(dbContext, materialization.CursorKey, true, token);
            if (cursor is null)
            {
                return new RecurringMaterializationResult { Status = RecurringMaterializationStatus.CursorNotFound };
            }
            var scope = await dbContext.CatalogScopes.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == materialization.CursorKey.SchedulerScopeKey,
                token);
            if (scope?.ActiveReleaseId is null
                || scope.ActiveIntentEpoch != scope.DesiredIntentEpoch
                || scope.ActivationEpoch != materialization.CursorKey.ActivationEpoch)
            {
                return new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.InactiveActivation,
                    Cursor = ToRecurringCursor(cursor)
                };
            }
            var template = Deserialize<JobExecutionTemplate>(cursor.TemplateJson);
            if (cursor.IsSuspended || await IsActiveJobDisabledAsync(dbContext, template.Revision, token))
            {
                if (!cursor.IsSuspended)
                {
                    cursor.IsSuspended = true;
                    cursor.NextOccurrenceUtcTicks = null;
                    cursor.LastSynchronizedChangeEpoch = scope.ChangeEpoch;
                    cursor.CursorVersion++;
                    cursor.UpdatedAtUtcTicks = ToTicks(await GetUtcNowAsync(dbContext, token));
                    cursor.ConcurrencyToken = NewVersion();
                }
                return new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.Suspended,
                    Cursor = ToRecurringCursor(cursor)
                };
            }
            if (cursor.CursorVersion != materialization.ExpectedVersion
                || cursor.NextOccurrenceUtcTicks != expectedTicks)
            {
                return new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.StaleCursor,
                    Cursor = ToRecurringCursor(cursor)
                };
            }

            var now = await GetUtcNowAsync(dbContext, token);
            if (expectedTicks > ToTicks(now))
            {
                return new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.NotDue,
                    Cursor = ToRecurringCursor(cursor)
                };
            }

            var request = new JobEnqueueRequest
            {
                InstanceId = materialization.InstanceId,
                SchedulerScopeKey = template.Revision.SchedulerScopeKey,
                JobKey = template.Revision.JobKey,
                AvailableAtUtc = FromTicks(expectedTicks),
                EnqueueReason = $"Recurring occurrence {FromTicks(expectedTicks):O} materialized"
            };
            var outstandingCount = await dbContext.Executions.CountAsync(
                item => item.SchedulerScopeKey == template.Revision.SchedulerScopeKey
                        && item.JobKey == template.Revision.JobKey
                        && (item.State == JobExecutionState.Queued || item.State == JobExecutionState.Running),
                token);
            JobExecutionSkipReason? skipReason = outstandingCount >= template.MaxConcurrency
                ? JobExecutionSkipReason.RecurringCapacityUnavailable
                : null;
            var execution = await EnqueueCapturedExecutionAsync(
                dbContext,
                request,
                template,
                now,
                token,
                JobExecutionOrigin.RecurringSchedule,
                FromTicks(expectedTicks),
                skipReason,
                skipReason is null
                    ? request.EnqueueReason
                    : $"Recurring occurrence {FromTicks(expectedTicks):O} skipped because {outstandingCount} outstanding "
                      + $"execution(s) reached the configured capacity of {template.MaxConcurrency}");
            cursor.NextOccurrenceUtcTicks = ToTicks(materialization.NextOccurrenceUtc);
            cursor.CursorVersion++;
            cursor.UpdatedAtUtcTicks = ToTicks(now);
            cursor.ConcurrencyToken = NewVersion();
            return new RecurringMaterializationResult
            {
                Status = RecurringMaterializationStatus.Materialized,
                Execution = execution,
                Cursor = ToRecurringCursor(cursor)
            };
        }, cancellationToken);
    }

    private async Task<bool> IsActiveJobDisabledAsync(
        JobSchedulerDbContext dbContext,
        JobRevisionIdentity revision,
        CancellationToken cancellationToken)
    {
        var scope = await dbContext.CatalogScopes.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == revision.SchedulerScopeKey,
            cancellationToken);
        if (scope?.ActiveReleaseId is null
            || scope.ActiveIntentEpoch != scope.DesiredIntentEpoch
            || scope.ActivationEpoch != revision.ActivationEpoch)
        {
            return true;
        }

        var release = await dbContext.CatalogReleases.AsNoTracking().SingleAsync(
            item => item.SchedulerScopeKey == revision.SchedulerScopeKey
                    && item.ReleaseId == scope.ActiveReleaseId,
            cancellationToken);
        var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
        if (!payload.OwnerSnapshots.TryGetValue(revision.OwnerKey, out var snapshot)
            || snapshot is null
            || !string.Equals(snapshot.WorkerRevisionId, revision.WorkerRevisionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Active catalog no longer contains worker revision '{revision.OwnerKey}/{revision.WorkerRevisionId}'.");
        }
        var declaration = snapshot.Declarations.SingleOrDefault(item =>
            string.Equals(item.JobKey, revision.JobKey, StringComparison.Ordinal));
        if (declaration is null)
        {
            throw new InvalidOperationException(
                $"Active catalog no longer contains job revision '{revision.JobRevisionId}'.");
        }
        var policy = await dbContext.JobPolicies.SingleAsync(item =>
            item.SchedulerScopeKey == revision.SchedulerScopeKey
            && item.JobKey == revision.JobKey,
            cancellationToken);
        var activeJobRevisionId = JobCatalogHash.ComputeJobRevision(
            snapshot.OwnerId,
            snapshot.WorkerRevisionId,
            declaration);
        if (!string.Equals(activeJobRevisionId, revision.JobRevisionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Active catalog no longer contains job revision '{revision.JobRevisionId}'.");
        }
        return policy.DisabledOverride ?? declaration.IsDisabledByDefault;
    }

    private static Task<JobRecurringCursorEntity?> LoadRecurringCursorAsync(
        JobSchedulerDbContext dbContext,
        RecurringScheduleCursorKey key,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<JobRecurringCursorEntity> query = dbContext.RecurringCursors;
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        return query.SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == key.SchedulerScopeKey
            && item.ActivationEpoch == key.ActivationEpoch
            && item.JobRevisionId == key.JobRevisionId,
            cancellationToken);
    }

    private static RecurringScheduleCursor ToRecurringCursor(JobRecurringCursorEntity entity) => new()
    {
        Key = new RecurringScheduleCursorKey
        {
            SchedulerScopeKey = entity.SchedulerScopeKey,
            ActivationEpoch = entity.ActivationEpoch,
            JobRevisionId = entity.JobRevisionId
        },
        Template = Deserialize<JobExecutionTemplate>(entity.TemplateJson),
        Schedule = Deserialize<RecurringScheduleDefinition>(entity.ScheduleJson),
        NextOccurrenceUtc = FromTicks(entity.NextOccurrenceUtcTicks),
        LastSynchronizedChangeEpoch = entity.LastSynchronizedChangeEpoch,
        IsSuspended = entity.IsSuspended,
        Version = entity.CursorVersion,
        UpdatedAtUtc = FromTicks(entity.UpdatedAtUtcTicks)
    };
}
