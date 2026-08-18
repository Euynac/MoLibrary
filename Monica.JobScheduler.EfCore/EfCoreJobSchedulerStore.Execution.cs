using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.EfCore;

public sealed partial class EfCoreJobSchedulerStore
{
    private const int CLEANUP_JOB_KEY_PAGE_SIZE = 256;

    /// <inheritdoc />
    public Task<JobExecutionInstance> EnqueueAsync(
        JobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        return WriteAsync(async (dbContext, token) =>
        {
            var existing = await LoadExecutionAsync(
                dbContext,
                request.SchedulerScopeKey,
                request.InstanceId,
                true,
                false,
                token);
            if (existing is not null)
            {
                return ResolveIdempotentEnqueue(request, existing, JobExecutionOrigin.Triggered);
            }

            var template = await ResolveActiveExecutionTemplateAsync(dbContext, request, token);
            return await EnqueueCapturedExecutionAsync(
                dbContext,
                request,
                template,
                await GetUtcNowAsync(dbContext, token),
                token,
                JobExecutionOrigin.Triggered);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobExecutionInstance> RunRecurringNowAsync(
        JobRecurringRunNowCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        var request = JobExecutionAdmission.CreateRecurringRunNowRequest(command);
        return WriteAsync(async (dbContext, token) =>
        {
            var existing = await LoadExecutionAsync(
                dbContext,
                command.SchedulerScopeKey,
                command.InstanceId,
                true,
                false,
                token);
            if (existing is not null)
            {
                return ResolveIdempotentEnqueue(request, existing, JobExecutionOrigin.RecurringRunNow);
            }

            var template = await ResolveActiveRecurringExecutionTemplateAsync(dbContext, command, token);
            return await EnqueueCapturedExecutionAsync(
                dbContext,
                request,
                template,
                await GetUtcNowAsync(dbContext, token),
                token,
                JobExecutionOrigin.RecurringRunNow);
        }, cancellationToken);
    }

    private static JobExecutionInstance ResolveIdempotentEnqueue(
        JobEnqueueRequest request,
        JobExecutionEntity existing,
        JobExecutionOrigin expectedOrigin)
    {
        var revision = Deserialize<JobExecutionTemplate>(existing.TemplateJson).Revision;
        var expectedTemplateMatches = string.Equals(request.JobKey, revision.JobKey, StringComparison.Ordinal)
                                      && (request.ExpectedOwnerId is null
                                          || string.Equals(
                                              request.ExpectedOwnerId,
                                              revision.OwnerKey,
                                              StringComparison.Ordinal))
                                      && (request.ExpectedJobRevisionId is null
                                          || string.Equals(
                                              request.ExpectedJobRevisionId,
                                              revision.JobRevisionId,
                                              StringComparison.Ordinal));
        if (!expectedTemplateMatches
            || existing.Origin != expectedOrigin
            || !string.Equals(existing.JobArgs, request.JobArgs, StringComparison.Ordinal)
            || (request.AvailableAtUtc is { } requestedAvailability
                && existing.AvailableAtUtcTicks != ToTicks(requestedAvailability)))
        {
            throw new InvalidOperationException(
                $"Execution identifier '{request.InstanceId}' was reused for a different enqueue request.");
        }

        return ToExecution(existing);
    }

    /// <inheritdoc />
    public Task<JobExecutionInstance?> GetExecutionAsync(
        string schedulerScopeKey,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(instanceId, nameof(instanceId));
        return ReadAsync(async (dbContext, token) =>
        {
            var entity = await LoadExecutionAsync(
                dbContext,
                schedulerScopeKey,
                instanceId,
                false,
                true,
                token);
            return entity is null ? null : ToExecutionDetail(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryResult<JobExecutionInstance>> QueryExecutionsAsync(
        JobExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        return ReadAsync(async (dbContext, token) =>
        {
            var filtered = dbContext.Executions.AsNoTracking()
                .Where(item => item.SchedulerScopeKey == query.SchedulerScopeKey);
            if (!string.IsNullOrWhiteSpace(query.JobKey))
            {
                filtered = filtered.Where(item => item.JobKey == query.JobKey);
            }
            if (query.States is { Count: > 0 })
            {
                filtered = filtered.Where(item => query.States.Contains(item.State));
            }
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var search = query.SearchText.ToLower();
                filtered = filtered.Where(item =>
                    item.InstanceId.ToLower().Contains(search) || item.JobKey.ToLower().Contains(search));
            }
            if (query.CreatedAfterUtc is { } createdAfter)
            {
                var ticks = ToTicks(createdAfter);
                filtered = filtered.Where(item => item.CreatedAtUtcTicks >= ticks);
            }
            if (query.CreatedBeforeUtc is { } createdBefore)
            {
                var ticks = ToTicks(createdBefore);
                filtered = filtered.Where(item => item.CreatedAtUtcTicks <= ticks);
            }

            var totalCount = await filtered.CountAsync(token);
            var ordered = query.SortField switch
            {
                JobExecutionSortField.CreatedAtUtc => query.SortDescending
                    ? filtered.OrderByDescending(item => item.CreatedAtUtcTicks).ThenByDescending(item => item.InstanceId)
                    : filtered.OrderBy(item => item.CreatedAtUtcTicks).ThenBy(item => item.InstanceId),
                JobExecutionSortField.AvailableAtUtc => query.SortDescending
                    ? filtered.OrderByDescending(item => item.AvailableAtUtcTicks).ThenByDescending(item => item.InstanceId)
                    : filtered.OrderBy(item => item.AvailableAtUtcTicks).ThenBy(item => item.InstanceId),
                JobExecutionSortField.StartedAtUtc => query.SortDescending
                    ? filtered.OrderByDescending(item => item.StartedAtUtcTicks).ThenByDescending(item => item.InstanceId)
                    : filtered.OrderBy(item => item.StartedAtUtcTicks).ThenBy(item => item.InstanceId),
                JobExecutionSortField.CompletedAtUtc => query.SortDescending
                    ? filtered.OrderByDescending(item => item.CompletedAtUtcTicks).ThenByDescending(item => item.InstanceId)
                    : filtered.OrderBy(item => item.CompletedAtUtcTicks).ThenBy(item => item.InstanceId),
                _ => throw new ArgumentOutOfRangeException(nameof(query), query.SortField, "Unsupported sort field.")
            };
            var entities = await ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(token);
            return new QueryResult<JobExecutionInstance>(entities.Select(ToExecution).ToList(), totalCount);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<JobExecutionState, int>> GetExecutionStateStatisticsAsync(
        string schedulerScopeKey,
        DateTimeOffset? startTimeUtc = null,
        DateTimeOffset? endTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var query = dbContext.Executions.AsNoTracking()
                .Where(item => item.SchedulerScopeKey == schedulerScopeKey);
            if (startTimeUtc is { } start)
            {
                var ticks = ToTicks(start);
                query = query.Where(item => item.CreatedAtUtcTicks >= ticks);
            }
            if (endTimeUtc is { } end)
            {
                var ticks = ToTicks(end);
                query = query.Where(item => item.CreatedAtUtcTicks <= ticks);
            }

            var grouped = await query.GroupBy(item => item.State)
                .Select(group => new { State = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.State, item => item.Count, token);
            return (IReadOnlyDictionary<JobExecutionState, int>)Enum.GetValues<JobExecutionState>()
                .ToDictionary(state => state, state => grouped.GetValueOrDefault(state));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JobExecutionInstance?>> GetLatestExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(jobKeys);
        var keys = jobKeys.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var jobKey in keys)
        {
            JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKeys));
        }
        return ReadAsync(async (dbContext, token) =>
        {
            var latest = new Dictionary<string, JobExecutionInstance?>(keys.Length, StringComparer.Ordinal);
            foreach (var jobKey in keys)
            {
                var entity = await dbContext.Executions.AsNoTracking()
                    .Where(item => item.SchedulerScopeKey == schedulerScopeKey && item.JobKey == jobKey)
                    .OrderByDescending(item => item.CreatedAtUtcTicks)
                    .ThenByDescending(item => item.InstanceId)
                    .FirstOrDefaultAsync(token);
                latest.Add(jobKey, entity is null ? null : ToExecution(entity));
            }

            return (IReadOnlyDictionary<string, JobExecutionInstance?>)latest;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetExecutionCleanupCandidatesAsync(
        string schedulerScopeKey,
        IReadOnlyDictionary<string, JobHistoryRetentionPolicy> retentionPolicies,
        int maxRetainedOrphanedExecutions,
        int maxDeletions,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(retentionPolicies);
        if (maxRetainedOrphanedExecutions < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetainedOrphanedExecutions),
                maxRetainedOrphanedExecutions,
                "Orphan retention cannot be negative.");
        }
        if (maxDeletions < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDeletions),
                maxDeletions,
                "The cleanup result bound must be greater than zero.");
        }

        return ReadAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var candidates = new Dictionary<string, long>(StringComparer.Ordinal);
            var jobKeyOffset = 0;
            while (true)
            {
                // Job keys are paged independently from execution rows. Each per-job query below is also capped by the
                // global result bound, so no cleanup pass materializes an unbounded terminal history.
                var jobKeys = await TerminalExecutions(dbContext, schedulerScopeKey)
                    .Select(static item => item.JobKey)
                    .Distinct()
                    .OrderBy(static jobKey => jobKey)
                    .Skip(jobKeyOffset)
                    .Take(CLEANUP_JOB_KEY_PAGE_SIZE)
                    .ToArrayAsync(token);
                foreach (var jobKey in jobKeys)
                {
                    var terminal = TerminalExecutions(dbContext, schedulerScopeKey)
                        .Where(item => item.JobKey == jobKey);
                    var hasPolicy = retentionPolicies.TryGetValue(jobKey, out var policy);
                    var maxRecords = hasPolicy ? policy!.MaxRecords : maxRetainedOrphanedExecutions;
                    if (maxRecords > 0)
                    {
                        var terminalCount = await terminal.CountAsync(token);
                        var countCandidateCount = Math.Min(maxDeletions, terminalCount - maxRecords);
                        if (countCandidateCount > 0)
                        {
                            var countCandidates = await OldestCleanupCandidates(terminal)
                                .Take(countCandidateCount)
                                .ToArrayAsync(token);
                            MergeCleanupCandidates(candidates, countCandidates, maxDeletions);
                        }
                    }

                    if (hasPolicy && policy!.MaxDays is > 0)
                    {
                        var cutoffTicks = ToTicks(now.AddDays(-policy.MaxDays.Value));
                        var ageCandidates = await OldestCleanupCandidates(terminal.Where(item =>
                                (item.CompletedAtUtcTicks ?? item.CreatedAtUtcTicks) < cutoffTicks))
                            .Take(maxDeletions)
                            .ToArrayAsync(token);
                        MergeCleanupCandidates(candidates, ageCandidates, maxDeletions);
                    }
                }

                if (jobKeys.Length < CLEANUP_JOB_KEY_PAGE_SIZE)
                {
                    break;
                }

                jobKeyOffset += jobKeys.Length;
            }

            return (IReadOnlyList<string>)candidates
                .OrderBy(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => pair.Key)
                .ToArray();
        }, cancellationToken);
    }

    private static IQueryable<JobExecutionEntity> TerminalExecutions(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey) =>
        dbContext.Executions.AsNoTracking()
            .Where(item => item.SchedulerScopeKey == schedulerScopeKey
                           && (item.State == JobExecutionState.Succeeded
                               || item.State == JobExecutionState.Failed
                               || item.State == JobExecutionState.Cancelled
                               || item.State == JobExecutionState.Skipped));

    private static IQueryable<ExecutionCleanupCandidate> OldestCleanupCandidates(
        IQueryable<JobExecutionEntity> terminal) =>
        terminal
            .OrderBy(item => item.CompletedAtUtcTicks ?? item.CreatedAtUtcTicks)
            .ThenBy(item => item.InstanceId)
            .Select(item => new ExecutionCleanupCandidate
            {
                InstanceId = item.InstanceId,
                SortTicks = item.CompletedAtUtcTicks ?? item.CreatedAtUtcTicks
            });

    private static void MergeCleanupCandidates(
        Dictionary<string, long> selected,
        IEnumerable<ExecutionCleanupCandidate> additions,
        int maxDeletions)
    {
        foreach (var addition in additions)
        {
            selected[addition.InstanceId] = addition.SortTicks;
        }
        if (selected.Count <= maxDeletions)
        {
            return;
        }

        var retained = selected
            .OrderBy(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(maxDeletions)
            .ToArray();
        selected.Clear();
        foreach (var pair in retained)
        {
            selected.Add(pair.Key, pair.Value);
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(instanceIds);
        var ids = instanceIds.Distinct(StringComparer.Ordinal).ToArray();
        return WriteAsync(async (dbContext, token) =>
        {
            var executions = await dbContext.Executions
                .Where(item => item.SchedulerScopeKey == schedulerScopeKey
                               && ids.Contains(item.InstanceId)
                               && (item.State == JobExecutionState.Succeeded
                                   || item.State == JobExecutionState.Failed
                                   || item.State == JobExecutionState.Cancelled
                                   || item.State == JobExecutionState.Skipped))
                .ToListAsync(token);
            dbContext.Executions.RemoveRange(executions);
            return executions.Count;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkerCapabilityLease> RegisterWorkerCapabilityAsync(
        WorkerCapabilityRegistration registration,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();
        ValidatePositiveDuration(leaseDuration, nameof(leaseDuration));
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var nowTicks = ToTicks(now);
            // Worker registration is the churn boundary that creates capability rows, so it also removes expired rows
            // for the same scope without introducing a separate cleanup service.
            await dbContext.WorkerCapabilities
                .Where(item => item.SchedulerScopeKey == registration.SchedulerScopeKey
                               && item.LeaseExpiresAtUtcTicks <= nowTicks)
                .ExecuteDeleteAsync(token);
            var entity = await dbContext.WorkerCapabilities.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == registration.SchedulerScopeKey
                        && item.WorkerInstanceId == registration.WorkerInstanceId,
                token);
            if (entity is null)
            {
                entity = new JobWorkerCapabilityEntity
                {
                    SchedulerScopeKey = registration.SchedulerScopeKey,
                    WorkerInstanceId = registration.WorkerInstanceId,
                    ConcurrencyToken = NewVersion()
                };
                dbContext.WorkerCapabilities.Add(entity);
            }
            else
            {
                entity.ConcurrencyToken = NewVersion();
            }

            entity.OwnerKey = registration.OwnerKey;
            entity.WorkerRevisionId = registration.WorkerRevisionId;
            entity.JobRevisionIdsJson = Serialize(registration.JobRevisionIds.Order(StringComparer.Ordinal).ToArray());
            entity.LeaseToken = NewToken();
            entity.LeaseExpiresAtUtcTicks = ToTicks(now.Add(leaseDuration));
            return ToCapability(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkerCapabilityLease?> RenewWorkerCapabilityAsync(
        WorkerCapabilityLeaseKey leaseKey,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        ValidatePositiveDuration(leaseDuration, nameof(leaseDuration));
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var entity = await LoadCurrentCapabilityAsync(dbContext, leaseKey, now, token);
            if (entity is null)
            {
                return null;
            }

            entity.LeaseExpiresAtUtcTicks = ToTicks(now.Add(leaseDuration));
            entity.ConcurrencyToken = NewVersion();
            return ToCapability(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ReleaseWorkerCapabilityAsync(
        WorkerCapabilityLeaseKey leaseKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        return WriteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.WorkerCapabilities.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == leaseKey.SchedulerScopeKey
                        && item.WorkerInstanceId == leaseKey.WorkerInstanceId
                        && item.LeaseToken == leaseKey.LeaseToken,
                token);
            if (entity is null)
            {
                return false;
            }

            dbContext.WorkerCapabilities.Remove(entity);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkerCapabilityLease>> GetActiveWorkerCapabilitiesAsync(
        string schedulerScopeKey,
        string? ownerKey = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var nowTicks = ToTicks(await GetUtcNowAsync(dbContext, token));
            var query = dbContext.WorkerCapabilities.AsNoTracking()
                .Where(item => item.SchedulerScopeKey == schedulerScopeKey && item.LeaseExpiresAtUtcTicks > nowTicks);
            if (ownerKey is not null)
            {
                query = query.Where(item => item.OwnerKey == ownerKey);
            }

            var entities = await query.OrderBy(item => item.OwnerKey)
                .ThenBy(item => item.WorkerInstanceId)
                .ToArrayAsync(token);
            return (IReadOnlyList<WorkerCapabilityLease>)entities.Select(ToCapability).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionLease>> ClaimAsync(
        JobClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var capability = await LoadCurrentCapabilityAsync(dbContext, request.CapabilityLeaseKey, now, token)
                ?? throw new InvalidOperationException(
                    $"Worker capability lease for '{request.CapabilityLeaseKey.WorkerInstanceId}' is expired or fenced.");
            var revisionIds = Deserialize<string[]>(capability.JobRevisionIdsJson);
            var nowTicks = ToTicks(now);
            var currentActivationEpoch = await dbContext.CatalogScopes.AsNoTracking()
                .Where(item => item.SchedulerScopeKey == capability.SchedulerScopeKey)
                .Select(item => item.ActiveIntentEpoch == item.DesiredIntentEpoch ? item.ActivationEpoch : 0)
                .SingleOrDefaultAsync(token);
            var eligibleExecutions = dbContext.Executions
                .Where(item => item.SchedulerScopeKey == capability.SchedulerScopeKey
                               && item.State == JobExecutionState.Queued
                               && item.ActivationEpoch == currentActivationEpoch
                               && item.AvailableAtUtcTicks <= nowTicks
                               && item.OwnerKey == capability.OwnerKey
                               && item.WorkerRevisionId == capability.WorkerRevisionId
                               && revisionIds.Contains(item.JobRevisionId));
            var candidateBudget = request.GetCandidateBudget();

            // The fair pass takes only the oldest eligible row for each logical job. Consequently a deep queue for
            // one saturated gate consumes one candidate, leaving other JobKeys visible within the fixed budget.
            var fairCandidates = await eligibleExecutions
                .Where(candidate => candidate.InstanceId == eligibleExecutions
                    .Where(item => item.JobKey == candidate.JobKey)
                    .OrderBy(item => item.AvailableAtUtcTicks)
                    .ThenBy(item => item.CreatedAtUtcTicks)
                    .ThenBy(item => item.InstanceId)
                    .Select(item => item.InstanceId)
                    .First())
                .OrderBy(item => item.AvailableAtUtcTicks)
                .ThenBy(item => item.CreatedAtUtcTicks)
                .ThenBy(item => item.InstanceId)
                .Take(candidateBudget)
                .ToArrayAsync(token);

            // Spend the remaining budget on globally oldest rows. This preserves batch throughput for jobs whose
            // concurrency limit is greater than one without allowing backlog depth to increase work per claim.
            var remainingCandidateBudget = candidateBudget - fairCandidates.Length;
            var fairCandidateIds = fairCandidates.Select(item => item.InstanceId).ToArray();
            var fillCandidates = remainingCandidateBudget > 0 && fairCandidates.Length > 0
                ? await eligibleExecutions
                    .Where(item => !fairCandidateIds.Contains(item.InstanceId))
                    .OrderBy(item => item.AvailableAtUtcTicks)
                    .ThenBy(item => item.CreatedAtUtcTicks)
                    .ThenBy(item => item.InstanceId)
                    .Take(remainingCandidateBudget)
                    .ToArrayAsync(token)
                : [];
            var candidates = fairCandidates.Concat(fillCandidates).ToArray();
            var candidateJobKeys = candidates
                .Select(item => item.JobKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var gatesByJobKey = (await dbContext.ExecutionGates
                    .Where(item => item.SchedulerScopeKey == capability.SchedulerScopeKey
                                   && candidateJobKeys.Contains(item.JobKey))
                    .ToArrayAsync(token))
                .ToDictionary(item => item.JobKey, StringComparer.Ordinal);
            var leases = new List<JobExecutionLease>(request.MaxCount);
            foreach (var execution in candidates)
            {
                if (leases.Count == request.MaxCount)
                {
                    break;
                }

                if (!gatesByJobKey.TryGetValue(execution.JobKey, out var gate))
                {
                    throw new InvalidOperationException(
                        $"Execution gate '{execution.JobKey}' was not initialized by catalog activation.");
                }
                if (gate.ActiveCount >= gate.MaxConcurrency)
                {
                    continue;
                }

                gate.ActiveCount++;
                gate.ConcurrencyToken = NewVersion();
                execution.State = JobExecutionState.Running;
                execution.StartedAtUtcTicks = nowTicks;
                execution.CompletedAtUtcTicks = null;
                execution.ExecutionAttempt++;
                execution.RunningWorkerInstanceId = capability.WorkerInstanceId;
                execution.ExecutionLeaseToken = NewToken();
                execution.CapabilityLeaseToken = capability.LeaseToken;
                execution.LeaseExpiresAtUtcTicks = ToTicks(now.Add(request.LeaseDuration));
                execution.CancellationRequestedAtUtcTicks = null;
                execution.ConcurrencyToken = NewVersion();
                AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                    $"Claimed by worker '{capability.WorkerInstanceId}'", capability.WorkerInstanceId,
                    JobExecutionState.Queued, JobExecutionState.Running);
                leases.Add(new JobExecutionLease
                {
                    Execution = ToExecution(execution),
                    LeaseKey = new JobLeaseKey
                    {
                        SchedulerScopeKey = execution.SchedulerScopeKey,
                        InstanceId = execution.InstanceId,
                        WorkerInstanceId = capability.WorkerInstanceId,
                        LeaseToken = execution.ExecutionLeaseToken
                    }
                });
            }

            return (IReadOnlyList<JobExecutionLease>)leases;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobLeaseRenewalResult> RenewLeaseAsync(
        JobLeaseKey leaseKey,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        ValidatePositiveDuration(leaseDuration, nameof(leaseDuration));
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var execution = await LoadCurrentExecutionLeaseAsync(dbContext, leaseKey, now, token);
            if (execution is null)
            {
                return new JobLeaseRenewalResult { Status = JobLeaseRenewalStatus.Lost };
            }

            execution.LeaseExpiresAtUtcTicks = ToTicks(now.Add(leaseDuration));
            execution.ConcurrencyToken = NewVersion();
            return new JobLeaseRenewalResult
            {
                Status = execution.CancellationRequestedAtUtcTicks.HasValue
                    ? JobLeaseRenewalStatus.CancellationRequested
                    : JobLeaseRenewalStatus.Active,
                LeaseExpiresAtUtc = FromTicks(execution.LeaseExpiresAtUtcTicks)
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobAttemptCompletionResult> CompleteAttemptAsync(
        JobAttemptCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);
        completion.Validate();
        return WriteAsync(async (dbContext, token) =>
            await CompleteAttemptAsync(dbContext, completion, await GetUtcNowAsync(dbContext, token), token),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobAttemptCompletionResult> ReleaseLeaseAsync(
        JobLeaseKey leaseKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        return CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = leaseKey,
            Outcome = JobAttemptOutcome.Abandoned,
            Message = "Worker released the execution after user code exited"
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCancellationResult> RequestCancellationAsync(
        string schedulerScopeKey,
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(instanceId, nameof(instanceId));
        return WriteAsync(async (dbContext, token) =>
        {
            var execution = await LoadExecutionAsync(
                dbContext,
                schedulerScopeKey,
                instanceId,
                true,
                false,
                token);
            if (execution is null)
            {
                return new JobCancellationResult { Status = JobCancellationStatus.NotFound };
            }
            if (IsTerminal(execution.State))
            {
                return new JobCancellationResult
                {
                    Status = JobCancellationStatus.AlreadyTerminal,
                    Execution = ToExecution(execution)
                };
            }

            var now = await GetUtcNowAsync(dbContext, token);
            var message = string.IsNullOrWhiteSpace(reason) ? "Cancellation requested" : reason;
            if (execution.State == JobExecutionState.Queued
                || execution.LeaseExpiresAtUtcTicks is null
                || execution.LeaseExpiresAtUtcTicks <= ToTicks(now))
            {
                var worker = execution.RunningWorkerInstanceId;
                if (execution.State == JobExecutionState.Running)
                {
                    await ReleaseExecutionGateAsync(dbContext, execution, token);
                }
                var previous = execution.State;
                execution.State = JobExecutionState.Cancelled;
                execution.CompletedAtUtcTicks = ToTicks(now);
                execution.CancellationRequestedAtUtcTicks = ToTicks(now);
                ClearExecutionLease(execution);
                execution.ConcurrencyToken = NewVersion();
                AddHistory(execution, now, JobExecutionHistoryKind.StateTransition, message, worker,
                    previous, JobExecutionState.Cancelled);
                return new JobCancellationResult
                {
                    Status = JobCancellationStatus.Cancelled,
                    Execution = ToExecution(execution)
                };
            }

            if (!execution.CancellationRequestedAtUtcTicks.HasValue)
            {
                execution.CancellationRequestedAtUtcTicks = ToTicks(now);
                execution.ConcurrencyToken = NewVersion();
                AddHistory(
                    execution,
                    now,
                    JobExecutionHistoryKind.Cancellation,
                    message,
                    execution.RunningWorkerInstanceId);
            }
            return new JobCancellationResult
            {
                Status = JobCancellationStatus.CancellationRequested,
                Execution = ToExecution(execution)
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobLeaseMutationStatus> AppendExecutionLogAsync(
        JobExecutionLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.LeaseKey);
        entry.LeaseKey.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Message);
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var execution = await LoadCurrentExecutionLeaseAsync(dbContext, entry.LeaseKey, now, token);
            if (execution is null)
            {
                return JobLeaseMutationStatus.Lost;
            }

            execution.ConcurrencyToken = NewVersion();
            AddHistory(execution, now, JobExecutionHistoryKind.ExecutionLog, entry.Message,
                entry.LeaseKey.WorkerInstanceId, logLevel: entry.LogLevel);
            return JobLeaseMutationStatus.Applied;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionInstance>> RecoverExpiredLeasesAsync(
        ExpiredLeaseRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentity(request.SchedulerScopeKey, nameof(request.SchedulerScopeKey));
        if (request.MaxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaxCount, "Recovery count must be greater than zero.");
        }

        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var nowTicks = ToTicks(now);
            var expired = await dbContext.Executions
                .Where(item => item.SchedulerScopeKey == request.SchedulerScopeKey
                               && item.State == JobExecutionState.Running
                               && item.LeaseExpiresAtUtcTicks <= nowTicks)
                .OrderBy(item => item.LeaseExpiresAtUtcTicks)
                .ThenBy(item => item.InstanceId)
                .Take(request.MaxCount)
                .ToArrayAsync(token);
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == request.SchedulerScopeKey,
                token);
            foreach (var execution in expired)
            {
                var worker = execution.RunningWorkerInstanceId;
                await ReleaseExecutionGateAsync(dbContext, execution, token);
                var superseded = scope is null || execution.ActivationEpoch != scope.ActivationEpoch;
                if (superseded || execution.CancellationRequestedAtUtcTicks.HasValue)
                {
                    execution.State = JobExecutionState.Cancelled;
                    execution.CompletedAtUtcTicks = nowTicks;
                    AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                        superseded
                            ? "Expired lease from a superseded catalog was cancelled"
                            : "Expired lease recovered after cancellation was requested",
                        worker,
                        JobExecutionState.Running, JobExecutionState.Cancelled);
                }
                else
                {
                    execution.State = JobExecutionState.Queued;
                    execution.AvailableAtUtcTicks = nowTicks;
                    execution.LeaseLossCount++;
                    AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                        "Expired execution lease recovered", worker,
                        JobExecutionState.Running, JobExecutionState.Queued);
                }
                ClearExecutionLease(execution);
                execution.ConcurrencyToken = NewVersion();
            }

            return (IReadOnlyList<JobExecutionInstance>)expired.Select(ToExecution).ToArray();
        }, cancellationToken);
    }

    private async Task<JobExecutionInstance> EnqueueCapturedExecutionAsync(
        JobSchedulerDbContext dbContext,
        JobEnqueueRequest request,
        JobExecutionTemplate template,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        JobExecutionOrigin origin,
        DateTimeOffset? recurringOccurrenceUtc = null,
        JobExecutionSkipReason? skipReason = null,
        string? historyMessage = null)
    {
        JobExecutionAdmission.Validate(template, origin, recurringOccurrenceUtc, skipReason);
        var availableAtUtc = request.AvailableAtUtc ?? now;
        long? recurringOccurrenceTicks = recurringOccurrenceUtc is { } occurrence
            ? ToTicks(occurrence)
            : null;
        var scope = template.Revision.SchedulerScopeKey;
        var existing = await LoadExecutionAsync(
            dbContext,
            scope,
            request.InstanceId,
            true,
            false,
            cancellationToken);
        if (existing is not null)
        {
            if (Deserialize<JobExecutionTemplate>(existing.TemplateJson) != template
                || !string.Equals(existing.JobArgs, request.JobArgs, StringComparison.Ordinal)
                || existing.Origin != origin
                || existing.RecurringOccurrenceUtcTicks != recurringOccurrenceTicks
                || existing.SkipReason != skipReason
                || (request.AvailableAtUtc is not null
                    && existing.AvailableAtUtcTicks != ToTicks(availableAtUtc)))
            {
                throw new InvalidOperationException(
                    $"Execution identifier '{request.InstanceId}' was reused for a different enqueue request.");
            }

            return ToExecution(existing);
        }

        var revision = template.Revision;
        var entity = new JobExecutionEntity
        {
            SchedulerScopeKey = revision.SchedulerScopeKey,
            InstanceId = request.InstanceId,
            TemplateJson = Serialize(template),
            CatalogReleaseId = revision.CatalogReleaseId,
            ActivationEpoch = revision.ActivationEpoch,
            OwnerKey = revision.OwnerKey,
            WorkerRevisionId = revision.WorkerRevisionId,
            JobRevisionId = revision.JobRevisionId,
            JobKey = revision.JobKey,
            JobArgs = request.JobArgs,
            Origin = origin,
            RecurringOccurrenceUtcTicks = recurringOccurrenceTicks,
            SkipReason = skipReason,
            AvailableAtUtcTicks = ToTicks(availableAtUtc),
            State = skipReason is null ? JobExecutionState.Queued : JobExecutionState.Skipped,
            CreatedAtUtcTicks = ToTicks(now),
            CompletedAtUtcTicks = skipReason is null ? null : ToTicks(now),
            ConcurrencyToken = NewVersion()
        };
        AddHistory(
            entity,
            now,
            JobExecutionHistoryKind.StateTransition,
            historyMessage ?? request.EnqueueReason,
            newState: entity.State,
            logLevel: skipReason is null ? LogLevel.Information : LogLevel.Warning);
        dbContext.Executions.Add(entity);
        return ToExecution(entity);
    }

    private async Task<JobExecutionTemplate> ResolveActiveExecutionTemplateAsync(
        JobSchedulerDbContext dbContext,
        JobEnqueueRequest request,
        CancellationToken cancellationToken)
    {
        var definition = await ResolveActiveDefinitionAsync(
            dbContext,
            request.SchedulerScopeKey,
            request.JobKey,
            request.ExpectedOwnerId,
            request.ExpectedJobRevisionId,
            cancellationToken);
        if (definition.IsDisabled)
        {
            throw new InvalidOperationException($"Active job '{request.JobKey}' is disabled.");
        }

        var template = definition.CreateExecutionTemplate();
        if (template.JobType != JobType.Triggered)
        {
            throw new InvalidOperationException(
                $"Active job '{request.JobKey}' is recurring and cannot be admitted through the triggered queue API.");
        }

        if (string.IsNullOrWhiteSpace(request.JobArgs))
        {
            throw new ArgumentException("Triggered job arguments cannot be empty.", nameof(request));
        }

        return template;
    }

    private async Task<JobExecutionTemplate> ResolveActiveRecurringExecutionTemplateAsync(
        JobSchedulerDbContext dbContext,
        JobRecurringRunNowCommand command,
        CancellationToken cancellationToken)
    {
        var definition = await ResolveActiveDefinitionAsync(
            dbContext,
            command.SchedulerScopeKey,
            command.JobKey,
            command.ExpectedOwnerId,
            command.ExpectedJobRevisionId,
            cancellationToken);
        var template = definition.CreateExecutionTemplate();
        if (template.JobType != JobType.Recurring)
        {
            throw new InvalidOperationException(
                $"Active job '{command.JobKey}' is triggered and cannot be admitted through the recurring run-now API.");
        }

        return template;
    }

    private async Task<ActiveJobDefinition> ResolveActiveDefinitionAsync(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey,
        string jobKey,
        string? expectedOwnerId,
        string? expectedJobRevisionId,
        CancellationToken cancellationToken)
    {
        var scope = await dbContext.CatalogScopes.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == schedulerScopeKey,
            cancellationToken);
        if (scope?.ActiveReleaseId is null)
        {
            throw new JobCatalogNotFoundException(
                $"Scheduler scope '{schedulerScopeKey}' has no active catalog.");
        }
        if (scope.ActiveIntentEpoch != scope.DesiredIntentEpoch)
        {
            throw new JobCatalogTransitionException(schedulerScopeKey);
        }

        var release = await GetReleaseAsync(
            dbContext,
            schedulerScopeKey,
            scope.ActiveReleaseId,
            cancellationToken);
        var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
        JobOwnerCatalogSnapshot? owner = null;
        JobDeclaration? declaration = null;
        foreach (var snapshot in payload.OwnerSnapshots.Values.OfType<JobOwnerCatalogSnapshot>())
        {
            var candidate = snapshot.Declarations.SingleOrDefault(item =>
                string.Equals(item.JobKey, jobKey, StringComparison.Ordinal));
            if (candidate is not null)
            {
                owner = snapshot;
                declaration = candidate;
                break;
            }
        }
        if (owner is null || declaration is null)
        {
            throw new JobCatalogNotFoundException(
                $"Active job '{jobKey}' was not found in scope '{schedulerScopeKey}'.");
        }

        var policy = await dbContext.JobPolicies.SingleAsync(item =>
            item.SchedulerScopeKey == schedulerScopeKey
            && item.JobKey == jobKey,
            cancellationToken);

        var definition = new ActiveJobDefinition
        {
            SchedulerScopeKey = schedulerScopeKey,
            ReleaseId = release.ReleaseId,
            ActivationEpoch = scope.ActivationEpoch,
            OwnerId = owner.OwnerId,
            WorkerRevisionId = owner.WorkerRevisionId,
            Declaration = declaration,
            Policy = ToPolicy(policy)
        };
        if ((expectedOwnerId is not null
             && !string.Equals(expectedOwnerId, definition.OwnerId, StringComparison.Ordinal))
            || (expectedJobRevisionId is not null
                && !string.Equals(expectedJobRevisionId, definition.JobRevisionId, StringComparison.Ordinal)))
        {
            throw new JobRevisionMismatchException(jobKey);
        }

        return definition;
    }

    private async Task<JobAttemptCompletionResult> CompleteAttemptAsync(
        JobSchedulerDbContext dbContext,
        JobAttemptCompletion completion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var execution = await LoadCurrentExecutionLeaseAsync(dbContext, completion.LeaseKey, now, cancellationToken);
        if (execution is null)
        {
            return new JobAttemptCompletionResult { Status = JobAttemptCompletionStatus.Lost };
        }

        var worker = execution.RunningWorkerInstanceId;
        await ReleaseExecutionGateAsync(dbContext, execution, cancellationToken);
        var outcome = execution.CancellationRequestedAtUtcTicks.HasValue ? JobAttemptOutcome.Cancelled : completion.Outcome;
        var nowTicks = ToTicks(now);
        switch (outcome)
        {
            case JobAttemptOutcome.Succeeded:
                execution.State = JobExecutionState.Succeeded;
                execution.CompletedAtUtcTicks = nowTicks;
                AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                    completion.Message ?? "Execution completed successfully", worker,
                    JobExecutionState.Running, JobExecutionState.Succeeded);
                break;
            case JobAttemptOutcome.Cancelled:
                execution.State = JobExecutionState.Cancelled;
                execution.CompletedAtUtcTicks = nowTicks;
                AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                    completion.Message ?? "Execution observed cancellation", worker,
                    JobExecutionState.Running, JobExecutionState.Cancelled);
                break;
            case JobAttemptOutcome.Abandoned:
                execution.State = JobExecutionState.Queued;
                execution.AvailableAtUtcTicks = nowTicks;
                AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                    completion.Message ?? "Execution released for another worker", worker,
                    JobExecutionState.Running, JobExecutionState.Queued);
                break;
            case JobAttemptOutcome.Failed:
                execution.RetryAttempt++;
                var template = Deserialize<JobExecutionTemplate>(execution.TemplateJson);
                if (execution.RetryAttempt <= template.RetryCount)
                {
                    execution.State = JobExecutionState.Queued;
                    execution.AvailableAtUtcTicks = ToTicks(now.Add(completion.RetryDelay));
                    AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                        completion.Message ?? $"Execution failed; retry {execution.RetryAttempt} queued", worker,
                        JobExecutionState.Running, JobExecutionState.Queued, LogLevel.Warning);
                }
                else
                {
                    execution.State = JobExecutionState.Failed;
                    execution.CompletedAtUtcTicks = nowTicks;
                    AddHistory(execution, now, JobExecutionHistoryKind.StateTransition,
                        completion.Message ?? "Execution failed and exhausted its retries", worker,
                        JobExecutionState.Running, JobExecutionState.Failed, LogLevel.Error);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(completion), completion.Outcome, "Unsupported outcome.");
        }

        ClearExecutionLease(execution);
        execution.ConcurrencyToken = NewVersion();
        return new JobAttemptCompletionResult
        {
            Status = JobAttemptCompletionStatus.Applied,
            Execution = ToExecution(execution)
        };
    }

    private static Task<JobWorkerCapabilityEntity?> LoadCurrentCapabilityAsync(
        JobSchedulerDbContext dbContext,
        WorkerCapabilityLeaseKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.WorkerCapabilities.SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == key.SchedulerScopeKey
            && item.WorkerInstanceId == key.WorkerInstanceId
            && item.LeaseToken == key.LeaseToken
            && item.LeaseExpiresAtUtcTicks > ToTicks(now), cancellationToken);

    private static async Task<JobExecutionEntity?> LoadCurrentExecutionLeaseAsync(
        JobSchedulerDbContext dbContext,
        JobLeaseKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var execution = await dbContext.Executions.SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == key.SchedulerScopeKey
            && item.InstanceId == key.InstanceId
            && item.State == JobExecutionState.Running
            && item.RunningWorkerInstanceId == key.WorkerInstanceId
            && item.ExecutionLeaseToken == key.LeaseToken
            && item.LeaseExpiresAtUtcTicks > ToTicks(now), cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var capability = await dbContext.WorkerCapabilities.AsNoTracking().SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == key.SchedulerScopeKey
            && item.WorkerInstanceId == key.WorkerInstanceId
            && item.LeaseToken == execution.CapabilityLeaseToken
            && item.LeaseExpiresAtUtcTicks > ToTicks(now), cancellationToken);
        return capability is null ? null : execution;
    }

    private static Task<JobExecutionEntity?> LoadExecutionAsync(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey,
        string instanceId,
        bool tracked,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        IQueryable<JobExecutionEntity> query = dbContext.Executions;
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        if (includeHistory)
        {
            query = query.Include(item => item.History);
        }
        return query.SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == schedulerScopeKey && item.InstanceId == instanceId, cancellationToken);
    }

    private static async Task ReleaseExecutionGateAsync(
        JobSchedulerDbContext dbContext,
        JobExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        var gate = await dbContext.ExecutionGates.SingleOrDefaultAsync(item =>
            item.SchedulerScopeKey == execution.SchedulerScopeKey
            && item.JobKey == execution.JobKey, cancellationToken)
            ?? throw new InvalidOperationException($"Execution gate '{execution.JobKey}' does not exist.");
        if (gate.ActiveCount < 1)
        {
            throw new InvalidOperationException($"Execution gate '{execution.JobKey}' has no active lease to release.");
        }
        gate.ActiveCount--;
        gate.ConcurrencyToken = NewVersion();
    }

    private static void ClearExecutionLease(JobExecutionEntity entity)
    {
        entity.RunningWorkerInstanceId = null;
        entity.ExecutionLeaseToken = null;
        entity.CapabilityLeaseToken = null;
        entity.LeaseExpiresAtUtcTicks = null;
        if (entity.State == JobExecutionState.Queued)
        {
            entity.CancellationRequestedAtUtcTicks = null;
        }
    }

    private void AddHistory(
        JobExecutionEntity execution,
        DateTimeOffset timestamp,
        JobExecutionHistoryKind kind,
        string message,
        string? workerInstanceId = null,
        JobExecutionState? previousState = null,
        JobExecutionState? newState = null,
        LogLevel logLevel = LogLevel.Information)
    {
        execution.History.Add(new JobExecutionHistoryEntity
        {
            SchedulerScopeKey = execution.SchedulerScopeKey,
            InstanceId = execution.InstanceId,
            Sequence = execution.NextHistorySequence++,
            TimestampUtcTicks = ToTicks(timestamp),
            Kind = kind,
            PreviousState = previousState,
            NewState = newState,
            LogLevel = logLevel,
            Message = _historyLimits.NormalizeMessage(message),
            WorkerInstanceId = workerInstanceId
        });
    }

    private static JobExecutionInstance ToExecution(JobExecutionEntity entity) => new()
    {
        InstanceId = entity.InstanceId,
        Template = Deserialize<JobExecutionTemplate>(entity.TemplateJson),
        JobArgs = entity.JobArgs,
        Origin = entity.Origin,
        RecurringOccurrenceUtc = FromTicks(entity.RecurringOccurrenceUtcTicks),
        SkipReason = entity.SkipReason,
        AvailableAtUtc = FromTicks(entity.AvailableAtUtcTicks),
        State = entity.State,
        CreatedAtUtc = FromTicks(entity.CreatedAtUtcTicks),
        StartedAtUtc = FromTicks(entity.StartedAtUtcTicks),
        CompletedAtUtc = FromTicks(entity.CompletedAtUtcTicks),
        ExecutionAttempt = entity.ExecutionAttempt,
        RetryAttempt = entity.RetryAttempt,
        LeaseLossCount = entity.LeaseLossCount,
        RunningWorkerInstanceId = entity.RunningWorkerInstanceId,
        LeaseExpiresAtUtc = FromTicks(entity.LeaseExpiresAtUtcTicks),
        CancellationRequestedAtUtc = FromTicks(entity.CancellationRequestedAtUtcTicks)
    };

    private static JobExecutionInstance ToExecutionDetail(JobExecutionEntity entity) =>
        ToExecution(entity) with
        {
            History = entity.History.OrderBy(item => item.Sequence).Select(item => new JobExecutionHistoryEntry
            {
                TimestampUtc = FromTicks(item.TimestampUtcTicks),
                Kind = item.Kind,
                PreviousState = item.PreviousState,
                NewState = item.NewState,
                LogLevel = item.LogLevel,
                Message = item.Message,
                WorkerInstanceId = item.WorkerInstanceId
            }).ToArray()
        };

    private static WorkerCapabilityLease ToCapability(JobWorkerCapabilityEntity entity) => new()
    {
        Capability = new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = entity.SchedulerScopeKey,
            OwnerKey = entity.OwnerKey,
            WorkerRevisionId = entity.WorkerRevisionId,
            WorkerInstanceId = entity.WorkerInstanceId,
            JobRevisionIds = Deserialize<string[]>(entity.JobRevisionIdsJson)
        },
        LeaseKey = new WorkerCapabilityLeaseKey
        {
            SchedulerScopeKey = entity.SchedulerScopeKey,
            WorkerInstanceId = entity.WorkerInstanceId,
            LeaseToken = entity.LeaseToken
        },
        LeaseExpiresAtUtc = FromTicks(entity.LeaseExpiresAtUtcTicks)
    };

    private sealed class ExecutionCleanupCandidate
    {
        public required string InstanceId { get; init; }
        public long SortTicks { get; init; }
    }

    private static bool IsTerminal(JobExecutionState state) =>
        state is JobExecutionState.Succeeded
            or JobExecutionState.Failed
            or JobExecutionState.Cancelled
            or JobExecutionState.Skipped;

    private static void ValidatePositiveDuration(TimeSpan duration, string parameterName)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, duration, "Duration must be greater than zero.");
        }
    }
}
