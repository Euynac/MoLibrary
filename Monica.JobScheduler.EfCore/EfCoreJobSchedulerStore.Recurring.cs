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

            var activeDefinition = await ResolveActiveRecurringDefinitionAsync(dbContext, revision, token);
            var activeSchedule = activeDefinition.EffectiveConfiguration.Schedule
                ?? throw new InvalidOperationException(
                    $"Recurring job '{revision.JobRevisionId}' did not resolve an effective schedule.");
            if (synchronization.Template != activeDefinition.CreateExecutionTemplate()
                || synchronization.Schedule != activeSchedule)
            {
                return new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.StaleChangeEpoch,
                    Cursor = cursor is null ? null : ToRecurringCursor(cursor)
                };
            }
            var suspensionReasons = synchronization.ResolveSuspensionReasons(activeDefinition.IsDisabled);
            if (cursor is not null)
            {
                var templateChanged = Deserialize<JobExecutionTemplate>(cursor.TemplateJson) != synchronization.Template;
                var scheduleChanged = Deserialize<RecurringScheduleDefinition>(cursor.ScheduleJson)
                                      != synchronization.Schedule;
                var policyRevisionChanged = !string.Equals(
                    cursor.AppliedPolicyRevision,
                    synchronization.Template.AppliedPolicyRevision,
                    StringComparison.Ordinal);
                if (cursor.LastSynchronizedChangeEpoch == synchronization.ChangeEpoch
                    && cursor.SuspensionReasons == suspensionReasons
                    && !templateChanged
                    && !scheduleChanged
                    && !policyRevisionChanged)
                {
                    return new RecurringScheduleSynchronizationResult
                    {
                        Status = RecurringScheduleSynchronizationStatus.Unchanged,
                        Cursor = ToRecurringCursor(cursor)
                    };
                }

                var now = await GetUtcNowAsync(dbContext, token);
                var resumed = cursor.SuspensionReasons != JobRecurringScheduleSuspensionReason.None
                              && suspensionReasons == JobRecurringScheduleSuspensionReason.None;
                cursor.TemplateJson = Serialize(synchronization.Template);
                cursor.ScheduleJson = Serialize(synchronization.Schedule);
                cursor.AppliedPolicyRevision = synchronization.Template.AppliedPolicyRevision;
                if (suspensionReasons != JobRecurringScheduleSuspensionReason.None)
                {
                    cursor.NextOccurrenceUtcTicks = null;
                }
                else if (scheduleChanged || resumed)
                {
                    cursor.NextOccurrenceUtcTicks = ToTicks(synchronization.Schedule.GetNextOccurrence(now));
                }
                cursor.SuspensionReasons = suspensionReasons;
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
            // Activation is the default durable admission boundary. A schedule replacement or resume persisted before
            // the first cursor advances that boundary, so a late first synchronization cannot replay old occurrences.
            var activationBoundaryUtc = FromTicks(await dbContext.CatalogActivations
                .AsNoTracking()
                .Where(item => item.SchedulerScopeKey == revision.SchedulerScopeKey
                               && item.ActivationEpoch == revision.ActivationEpoch)
                .Select(static item => item.ActivatedAtUtcTicks)
                .SingleAsync(token));
            var firstOccurrenceBoundaryUtc =
                activeDefinition.Policy.RecurringScheduleEffectiveFromUtc is { } effectiveFromUtc
                && effectiveFromUtc > activationBoundaryUtc
                ? effectiveFromUtc
                : activationBoundaryUtc;
            cursor = new JobRecurringCursorEntity
            {
                SchedulerScopeKey = revision.SchedulerScopeKey,
                ActivationEpoch = revision.ActivationEpoch,
                JobRevisionId = revision.JobRevisionId,
                TemplateJson = Serialize(synchronization.Template),
                ScheduleJson = Serialize(synchronization.Schedule),
                AppliedPolicyRevision = synchronization.Template.AppliedPolicyRevision,
                NextOccurrenceUtcTicks = suspensionReasons != JobRecurringScheduleSuspensionReason.None
                    ? null
                    : ToTicks(synchronization.Schedule.GetNextOccurrence(firstOccurrenceBoundaryUtc)),
                LastSynchronizedChangeEpoch = synchronization.ChangeEpoch,
                SuspensionReasons = suspensionReasons,
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
                               && item.SuspensionReasons == JobRecurringScheduleSuspensionReason.None
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
            var activeDefinition = await ResolveActiveRecurringDefinitionAsync(dbContext, template.Revision, token);
            var isSuspendedByOperatorPolicy = activeDefinition.IsDisabled;
            if (cursor.SuspensionReasons != JobRecurringScheduleSuspensionReason.None
                || isSuspendedByOperatorPolicy)
            {
                var suspensionReasons = cursor.SuspensionReasons;
                if (isSuspendedByOperatorPolicy)
                {
                    suspensionReasons |= JobRecurringScheduleSuspensionReason.OperatorPolicy;
                }

                if (cursor.SuspensionReasons != suspensionReasons
                    || cursor.NextOccurrenceUtcTicks is not null
                    || cursor.LastSynchronizedChangeEpoch < scope.ChangeEpoch)
                {
                    cursor.SuspensionReasons = suspensionReasons;
                    cursor.NextOccurrenceUtcTicks = null;
                    cursor.LastSynchronizedChangeEpoch = Math.Max(
                        cursor.LastSynchronizedChangeEpoch,
                        scope.ChangeEpoch);
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
            if (!string.Equals(
                    cursor.AppliedPolicyRevision,
                    activeDefinition.Policy.ConcurrencyStamp,
                    StringComparison.Ordinal)
                || !string.Equals(
                    template.AppliedPolicyRevision,
                    activeDefinition.Policy.ConcurrencyStamp,
                    StringComparison.Ordinal))
            {
                return new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.StaleCursor,
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
            var gate = await dbContext.ExecutionGates.SingleAsync(
                item => item.SchedulerScopeKey == template.Revision.SchedulerScopeKey
                        && item.JobKey == template.Revision.JobKey,
                token);
            JobExecutionSkipReason? skipReason = outstandingCount >= gate.MaxConcurrency
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
                      + $"execution(s) reached the configured capacity of {gate.MaxConcurrency}");
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

    private async Task<ActiveJobDefinition> ResolveActiveRecurringDefinitionAsync(
        JobSchedulerDbContext dbContext,
        JobRevisionIdentity revision,
        CancellationToken cancellationToken)
    {
        var definition = await ResolveActiveDefinitionAsync(
            dbContext,
            revision.SchedulerScopeKey,
            revision.JobKey,
            revision.OwnerKey,
            revision.JobRevisionId,
            cancellationToken);
        if (definition.ActivationEpoch != revision.ActivationEpoch)
        {
            throw new InvalidOperationException(
                $"Job revision '{revision.JobRevisionId}' no longer belongs to the active catalog.");
        }

        return definition;
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
        AppliedPolicyRevision = entity.AppliedPolicyRevision,
        NextOccurrenceUtc = FromTicks(entity.NextOccurrenceUtcTicks),
        LastSynchronizedChangeEpoch = entity.LastSynchronizedChangeEpoch,
        SuspensionReasons = entity.SuspensionReasons,
        Version = entity.CursorVersion,
        UpdatedAtUtc = FromTicks(entity.UpdatedAtUtcTicks)
    };
}
