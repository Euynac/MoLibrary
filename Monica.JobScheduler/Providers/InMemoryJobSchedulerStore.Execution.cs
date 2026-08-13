using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Providers;

/// <summary>
/// Implements the execution half of the volatile scheduler store.
/// </summary>
public sealed partial class InMemoryJobSchedulerStore
{
    private readonly Dictionary<ExecutionKey, StoredExecution> _executionInstances = [];
    private readonly Dictionary<ExecutionGateKey, int> _executionGateCounts = [];
    private readonly Dictionary<WorkerCapabilityKey, StoredWorkerCapability> _workerCapabilityLeases = [];
    private readonly Dictionary<RecurringCursorStorageKey, StoredRecurringCursor> _recurringCursors = [];

    /// <inheritdoc />
    public Task<JobExecutionInstance> EnqueueAsync(
        JobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var executionKey = new ExecutionKey(request.SchedulerScopeKey, request.InstanceId);
            if (_executionInstances.TryGetValue(executionKey, out var existing))
            {
                return Task.FromResult(ResolveIdempotentEnqueueUnsafe(request, existing));
            }

            var template = ResolveActiveExecutionTemplateUnsafe(request);
            return Task.FromResult(EnqueueCapturedUnsafe(request, template, UtcNow));
        }
    }

    /// <inheritdoc />
    public Task<JobExecutionInstance?> GetExecutionAsync(
        string schedulerScopeKey,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(instanceId, nameof(instanceId));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var key = new ExecutionKey(schedulerScopeKey, instanceId);
            return Task.FromResult(_executionInstances.TryGetValue(key, out var execution)
                ? ToSnapshot(execution)
                : null);
        }
    }

    /// <inheritdoc />
    public Task<QueryResult<JobExecutionInstance>> QueryExecutionsAsync(
        JobExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IEnumerable<StoredExecution> filtered = _executionInstances.Values
                .Where(execution => string.Equals(
                    execution.Template.Revision.SchedulerScopeKey,
                    query.SchedulerScopeKey,
                    StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(query.JobKey))
            {
                filtered = filtered.Where(execution => string.Equals(
                    execution.Template.Revision.JobKey,
                    query.JobKey,
                    StringComparison.Ordinal));
            }

            if (query.States is { Count: > 0 })
            {
                var states = query.States.ToHashSet();
                filtered = filtered.Where(execution => states.Contains(execution.State));
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                filtered = filtered.Where(execution =>
                    execution.InstanceId.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                    || execution.Template.Revision.JobKey.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (query.CreatedAfterUtc is { } createdAfter)
            {
                var normalized = NormalizeUtc(createdAfter);
                filtered = filtered.Where(execution => execution.CreatedAtUtc >= normalized);
            }

            if (query.CreatedBeforeUtc is { } createdBefore)
            {
                var normalized = NormalizeUtc(createdBefore);
                filtered = filtered.Where(execution => execution.CreatedAtUtc <= normalized);
            }

            var materialized = filtered.ToList();
            var totalCount = materialized.Count;
            var ordered = OrderExecutions(materialized, query.SortField, query.SortDescending);
            var page = ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToSummary)
                .ToList();

            return Task.FromResult(new QueryResult<JobExecutionInstance>(page, totalCount));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<JobExecutionState, int>> GetExecutionStateStatisticsAsync(
        string schedulerScopeKey,
        DateTimeOffset? startTimeUtc = null,
        DateTimeOffset? endTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var executions = _executionInstances.Values.Where(execution =>
                string.Equals(execution.Template.Revision.SchedulerScopeKey, schedulerScopeKey, StringComparison.Ordinal));
            if (startTimeUtc is { } start)
            {
                var normalized = NormalizeUtc(start);
                executions = executions.Where(execution => execution.CreatedAtUtc >= normalized);
            }

            if (endTimeUtc is { } end)
            {
                var normalized = NormalizeUtc(end);
                executions = executions.Where(execution => execution.CreatedAtUtc <= normalized);
            }

            var grouped = executions
                .GroupBy(execution => execution.State)
                .ToDictionary(group => group.Key, group => group.Count());
            IReadOnlyDictionary<JobExecutionState, int> result = Enum
                .GetValues<JobExecutionState>()
                .ToDictionary(state => state, state => grouped.GetValueOrDefault(state));
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JobExecutionInstance?>> GetLatestExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(jobKeys);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedKeys = jobKeys.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var jobKey in requestedKeys)
        {
            JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKeys));
        }
        lock (_gate)
        {
            IReadOnlyDictionary<string, JobExecutionInstance?> result = requestedKeys.ToDictionary(
                jobKey => jobKey,
                jobKey => _executionInstances.Values
                    .Where(execution =>
                        string.Equals(
                            execution.Template.Revision.SchedulerScopeKey,
                            schedulerScopeKey,
                            StringComparison.Ordinal)
                        && string.Equals(execution.Template.Revision.JobKey, jobKey, StringComparison.Ordinal))
                    .OrderByDescending(execution => execution.CreatedAtUtc)
                    .ThenByDescending(execution => execution.InstanceId, StringComparer.Ordinal)
                    .Select(ToSummary)
                    .FirstOrDefault(),
                StringComparer.Ordinal);
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetExecutionCleanupCandidatesAsync(
        string schedulerScopeKey,
        IReadOnlyDictionary<string, JobHistoryRetentionPolicy> retentionPolicies,
        int maxRetainedOrphanedExecutions,
        int maxDeletions,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            var candidates = new Dictionary<string, StoredExecution>(StringComparer.Ordinal);
            var terminalGroups = _executionInstances.Values
                .Where(execution =>
                    string.Equals(
                        execution.Template.Revision.SchedulerScopeKey,
                        schedulerScopeKey,
                        StringComparison.Ordinal)
                    && IsTerminal(execution.State))
                .GroupBy(execution => execution.Template.Revision.JobKey, StringComparer.Ordinal);

            foreach (var group in terminalGroups)
            {
                var ordered = group
                    .OrderByDescending(execution => execution.CompletedAtUtc ?? execution.CreatedAtUtc)
                    .ThenByDescending(execution => execution.InstanceId, StringComparer.Ordinal)
                    .ToArray();
                var hasPolicy = retentionPolicies.TryGetValue(group.Key, out var policy);
                var maxRecords = hasPolicy
                    ? policy!.MaxRecords
                    : maxRetainedOrphanedExecutions;
                var cutoff = hasPolicy && policy!.MaxDays is > 0
                    ? now.AddDays(-policy.MaxDays.Value)
                    : (DateTimeOffset?)null;

                if (maxRecords > 0)
                {
                    MergeCleanupCandidatesUnsafe(
                        candidates,
                        ordered.Skip(maxRecords)
                            .OrderBy(execution => execution.CompletedAtUtc ?? execution.CreatedAtUtc)
                            .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal)
                            .Take(maxDeletions),
                        maxDeletions);
                }

                if (cutoff is { } cutoffUtc)
                {
                    MergeCleanupCandidatesUnsafe(
                        candidates,
                        ordered.Where(execution =>
                                (execution.CompletedAtUtc ?? execution.CreatedAtUtc) < cutoffUtc)
                            .OrderBy(execution => execution.CompletedAtUtc ?? execution.CreatedAtUtc)
                            .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal)
                            .Take(maxDeletions),
                        maxDeletions);
                }
            }

            IReadOnlyList<string> result = candidates
                .Values
                .OrderBy(execution => execution.CompletedAtUtc ?? execution.CreatedAtUtc)
                .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal)
                .Select(execution => execution.InstanceId)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private static void MergeCleanupCandidatesUnsafe(
        Dictionary<string, StoredExecution> selected,
        IEnumerable<StoredExecution> additions,
        int maxDeletions)
    {
        foreach (var addition in additions)
        {
            selected[addition.InstanceId] = addition;
        }
        if (selected.Count <= maxDeletions)
        {
            return;
        }

        var retained = selected.Values
            .OrderBy(execution => execution.CompletedAtUtc ?? execution.CreatedAtUtc)
            .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal)
            .Take(maxDeletions)
            .ToArray();
        selected.Clear();
        foreach (var execution in retained)
        {
            selected.Add(execution.InstanceId, execution);
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(instanceIds);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var deleted = 0;
            foreach (var instanceId in instanceIds.Distinct(StringComparer.Ordinal))
            {
                JobSchedulerIdentity.ValidateStandard(instanceId, nameof(instanceIds));
                var key = new ExecutionKey(schedulerScopeKey, instanceId);
                if (_executionInstances.TryGetValue(key, out var execution)
                    && IsTerminal(execution.State)
                    && _executionInstances.Remove(key))
                {
                    deleted++;
                }
            }

            return Task.FromResult(deleted);
        }
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            // Keep parity with the durable store: registration bounds stale capability state for its own scope.
            var expiredKeys = _workerCapabilityLeases
                .Where(pair => string.Equals(
                                   pair.Key.SchedulerScopeKey,
                                   registration.SchedulerScopeKey,
                                   StringComparison.Ordinal)
                               && pair.Value.LeaseExpiresAtUtc <= now)
                .Select(static pair => pair.Key)
                .ToArray();
            foreach (var expiredKey in expiredKeys)
            {
                _workerCapabilityLeases.Remove(expiredKey);
            }

            var stored = new StoredWorkerCapability
            {
                SchedulerScopeKey = registration.SchedulerScopeKey,
                OwnerKey = registration.OwnerKey,
                WorkerRevisionId = registration.WorkerRevisionId,
                WorkerInstanceId = registration.WorkerInstanceId,
                JobRevisionIds = registration.JobRevisionIds.ToHashSet(StringComparer.Ordinal),
                LeaseToken = NewToken(),
                LeaseExpiresAtUtc = now.Add(leaseDuration)
            };
            _workerCapabilityLeases[new WorkerCapabilityKey(
                registration.SchedulerScopeKey,
                registration.WorkerInstanceId)] = stored;
            return Task.FromResult(ToSnapshot(stored));
        }
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            if (!TryGetCurrentCapabilityUnsafe(leaseKey, now, out var capability))
            {
                return Task.FromResult<WorkerCapabilityLease?>(null);
            }

            capability.LeaseExpiresAtUtc = now.Add(leaseDuration);
            return Task.FromResult<WorkerCapabilityLease?>(ToSnapshot(capability));
        }
    }

    /// <inheritdoc />
    public Task<bool> ReleaseWorkerCapabilityAsync(
        WorkerCapabilityLeaseKey leaseKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var key = new WorkerCapabilityKey(leaseKey.SchedulerScopeKey, leaseKey.WorkerInstanceId);
            var released = _workerCapabilityLeases.TryGetValue(key, out var capability)
                           && string.Equals(capability.LeaseToken, leaseKey.LeaseToken, StringComparison.Ordinal)
                           && _workerCapabilityLeases.Remove(key);
            return Task.FromResult(released);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkerCapabilityLease>> GetActiveWorkerCapabilitiesAsync(
        string schedulerScopeKey,
        string? ownerKey = null,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        if (ownerKey is not null)
        {
            JobSchedulerIdentity.ValidateStandard(ownerKey, nameof(ownerKey));
        }
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            IReadOnlyList<WorkerCapabilityLease> result = _workerCapabilityLeases.Values
                .Where(capability =>
                    capability.LeaseExpiresAtUtc > now
                    && string.Equals(capability.SchedulerScopeKey, schedulerScopeKey, StringComparison.Ordinal)
                    && (ownerKey is null
                        || string.Equals(capability.OwnerKey, ownerKey, StringComparison.Ordinal)))
                .OrderBy(capability => capability.OwnerKey, StringComparer.Ordinal)
                .ThenBy(capability => capability.WorkerInstanceId, StringComparer.Ordinal)
                .Select(ToSnapshot)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionLease>> ClaimAsync(
        JobClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            if (!TryGetCurrentCapabilityUnsafe(request.CapabilityLeaseKey, now, out var capability))
            {
                throw new InvalidOperationException(
                    $"Worker capability lease for '{request.CapabilityLeaseKey.WorkerInstanceId}' is expired or fenced.");
            }

            var eligibleExecutions = _executionInstances.Values
                .Where(execution =>
                    execution.State == JobExecutionState.Queued
                    && execution.AvailableAtUtc <= now
                    && IsStableActivation(
                        execution.Template.Revision.SchedulerScopeKey,
                        execution.Template.Revision.ActivationEpoch)
                    && IsCompatible(execution, capability))
                .OrderBy(execution => execution.AvailableAtUtc)
                .ThenBy(execution => execution.CreatedAtUtc)
                .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal);
            var candidateBudget = request.GetCandidateBudget();
            var fairCandidates = eligibleExecutions
                .DistinctBy(execution => execution.Template.Revision.JobKey, StringComparer.Ordinal)
                .Take(candidateBudget)
                .ToArray();
            var fairCandidateIds = fairCandidates
                .Select(execution => execution.InstanceId)
                .ToHashSet(StringComparer.Ordinal);
            var candidates = fairCandidates
                .Concat(eligibleExecutions
                    .Where(execution => !fairCandidateIds.Contains(execution.InstanceId))
                    .Take(candidateBudget - fairCandidates.Length))
                .ToArray();
            var leases = new List<JobExecutionLease>(request.MaxCount);

            foreach (var execution in candidates)
            {
                if (leases.Count == request.MaxCount)
                {
                    break;
                }

                var gateKey = GetExecutionGateKey(execution.Template.Revision);
                var activeCount = _executionGateCounts.GetValueOrDefault(gateKey);
                if (activeCount >= execution.Template.MaxConcurrency)
                {
                    continue;
                }

                _executionGateCounts[gateKey] = activeCount + 1;
                execution.State = JobExecutionState.Running;
                execution.StartedAtUtc = now;
                execution.CompletedAtUtc = null;
                execution.ExecutionAttempt++;
                execution.RunningWorkerInstanceId = capability.WorkerInstanceId;
                var executionLeaseToken = NewToken();
                execution.ExecutionLeaseToken = executionLeaseToken;
                execution.CapabilityLeaseToken = capability.LeaseToken;
                execution.LeaseExpiresAtUtc = now.Add(request.LeaseDuration);
                execution.CancellationRequestedAtUtc = null;
                AddHistory(execution, Transition(
                    now,
                    JobExecutionState.Queued,
                    JobExecutionState.Running,
                    $"Claimed by worker '{capability.WorkerInstanceId}'",
                    capability.WorkerInstanceId));

                leases.Add(new JobExecutionLease
                {
                    Execution = ToSummary(execution),
                    LeaseKey = new JobLeaseKey
                    {
                        SchedulerScopeKey = execution.Template.Revision.SchedulerScopeKey,
                        InstanceId = execution.InstanceId,
                        WorkerInstanceId = capability.WorkerInstanceId,
                        LeaseToken = executionLeaseToken
                    }
                });
            }

            return Task.FromResult<IReadOnlyList<JobExecutionLease>>(leases);
        }
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            if (!TryGetCurrentExecutionLeaseUnsafe(leaseKey, now, out var execution))
            {
                return Task.FromResult(new JobLeaseRenewalResult { Status = JobLeaseRenewalStatus.Lost });
            }

            execution.LeaseExpiresAtUtc = now.Add(leaseDuration);
            return Task.FromResult(new JobLeaseRenewalResult
            {
                Status = execution.CancellationRequestedAtUtc.HasValue
                    ? JobLeaseRenewalStatus.CancellationRequested
                    : JobLeaseRenewalStatus.Active,
                LeaseExpiresAtUtc = execution.LeaseExpiresAtUtc
            });
        }
    }

    /// <inheritdoc />
    public Task<JobAttemptCompletionResult> CompleteAttemptAsync(
        JobAttemptCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);
        completion.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(CompleteAttemptUnsafe(completion, UtcNow));
        }
    }

    /// <inheritdoc />
    public Task<JobAttemptCompletionResult> ReleaseLeaseAsync(
        JobLeaseKey leaseKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseKey);
        leaseKey.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(CompleteAttemptUnsafe(
                new JobAttemptCompletion
                {
                    LeaseKey = leaseKey,
                    Outcome = JobAttemptOutcome.Abandoned,
                    Message = "Worker released the execution after user code exited"
                },
                UtcNow));
        }
    }

    /// <inheritdoc />
    public Task<JobCancellationResult> RequestCancellationAsync(
        string schedulerScopeKey,
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(instanceId, nameof(instanceId));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var key = new ExecutionKey(schedulerScopeKey, instanceId);
            if (!_executionInstances.TryGetValue(key, out var execution))
            {
                return Task.FromResult(new JobCancellationResult { Status = JobCancellationStatus.NotFound });
            }

            if (IsTerminal(execution.State))
            {
                return Task.FromResult(new JobCancellationResult
                {
                    Status = JobCancellationStatus.AlreadyTerminal,
                    Execution = ToSummary(execution)
                });
            }

            var now = UtcNow;
            var message = string.IsNullOrWhiteSpace(reason) ? "Cancellation requested" : reason;
            if (execution.State == JobExecutionState.Queued
                || execution.LeaseExpiresAtUtc is null
                || execution.LeaseExpiresAtUtc <= now)
            {
                if (execution.State == JobExecutionState.Running)
                {
                    ReleaseExecutionGateUnsafe(execution);
                }

                var previousState = execution.State;
                var workerInstanceId = execution.RunningWorkerInstanceId;
                execution.State = JobExecutionState.Cancelled;
                execution.CompletedAtUtc = now;
                execution.CancellationRequestedAtUtc = now;
                ClearExecutionLeaseUnsafe(execution);
                AddHistory(execution, Transition(
                    now,
                    previousState,
                    JobExecutionState.Cancelled,
                    message,
                    workerInstanceId));
                return Task.FromResult(new JobCancellationResult
                {
                    Status = JobCancellationStatus.Cancelled,
                    Execution = ToSummary(execution)
                });
            }

            if (!execution.CancellationRequestedAtUtc.HasValue)
            {
                execution.CancellationRequestedAtUtc = now;
                AddHistory(execution, new JobExecutionHistoryEntry
                {
                    TimestampUtc = now,
                    Kind = JobExecutionHistoryKind.Cancellation,
                    Message = message,
                    WorkerInstanceId = execution.RunningWorkerInstanceId
                });
            }
            return Task.FromResult(new JobCancellationResult
            {
                Status = JobCancellationStatus.CancellationRequested,
                Execution = ToSummary(execution)
            });
        }
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = UtcNow;
            if (!TryGetCurrentExecutionLeaseUnsafe(entry.LeaseKey, now, out var execution))
            {
                return Task.FromResult(JobLeaseMutationStatus.Lost);
            }

            AddHistory(execution, new JobExecutionHistoryEntry
            {
                TimestampUtc = now,
                Kind = JobExecutionHistoryKind.ExecutionLog,
                LogLevel = entry.LogLevel,
                Message = entry.Message,
                WorkerInstanceId = entry.LeaseKey.WorkerInstanceId
            });
            return Task.FromResult(JobLeaseMutationStatus.Applied);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionInstance>> RecoverExpiredLeasesAsync(
        ExpiredLeaseRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        JobSchedulerIdentity.ValidateStandard(request.SchedulerScopeKey, nameof(request.SchedulerScopeKey));
        if (request.MaxCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaxCount,
                "Recovery count must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = UtcNow;
            var expired = _executionInstances.Values
                .Where(execution =>
                    execution.State == JobExecutionState.Running
                    && execution.LeaseExpiresAtUtc <= now
                    && string.Equals(
                        execution.Template.Revision.SchedulerScopeKey,
                        request.SchedulerScopeKey,
                        StringComparison.Ordinal))
                .OrderBy(execution => execution.LeaseExpiresAtUtc)
                .ThenBy(execution => execution.InstanceId, StringComparer.Ordinal)
                .Take(request.MaxCount)
                .ToArray();

            var recovered = new List<JobExecutionInstance>(expired.Length);
            foreach (var execution in expired)
            {
                var workerInstanceId = execution.RunningWorkerInstanceId;
                ReleaseExecutionGateUnsafe(execution);
                var activeEpoch = _catalogScopes[request.SchedulerScopeKey].ActivationEpoch;
                var isSuperseded = execution.Template.Revision.ActivationEpoch != activeEpoch;
                if (execution.CancellationRequestedAtUtc.HasValue || isSuperseded)
                {
                    execution.State = JobExecutionState.Cancelled;
                    execution.CompletedAtUtc = now;
                    AddHistory(execution, Transition(
                        now,
                        JobExecutionState.Running,
                        JobExecutionState.Cancelled,
                        isSuperseded
                            ? "Expired execution belonged to a superseded catalog activation"
                            : "Expired lease recovered after cancellation was requested",
                        workerInstanceId));
                }
                else
                {
                    execution.State = JobExecutionState.Queued;
                    execution.AvailableAtUtc = now;
                    execution.LeaseLossCount++;
                    AddHistory(execution, Transition(
                        now,
                        JobExecutionState.Running,
                        JobExecutionState.Queued,
                        "Expired execution lease recovered",
                        workerInstanceId));
                }

                ClearExecutionLeaseUnsafe(execution);
                recovered.Add(ToSummary(execution));
            }

            return Task.FromResult<IReadOnlyList<JobExecutionInstance>>(recovered);
        }
    }

    /// <inheritdoc />
    public Task<RecurringScheduleSynchronizationResult> SynchronizeRecurringScheduleAsync(
        RecurringScheduleSynchronization synchronization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        synchronization.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var revision = synchronization.Template.Revision;
            var key = new RecurringCursorStorageKey(
                revision.SchedulerScopeKey,
                revision.ActivationEpoch,
                revision.JobRevisionId);
            _recurringCursors.TryGetValue(key, out var existing);
            if (!IsStableActivation(revision.SchedulerScopeKey, revision.ActivationEpoch))
            {
                return Task.FromResult(new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.InactiveActivation,
                    Cursor = existing is null ? null : ToSnapshot(existing)
                });
            }

            var scope = _catalogScopes[revision.SchedulerScopeKey];
            if (synchronization.ChangeEpoch < scope.ChangeEpoch
                || existing is not null
                && synchronization.ChangeEpoch < existing.LastSynchronizedChangeEpoch)
            {
                return Task.FromResult(new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.StaleChangeEpoch,
                    Cursor = existing is null ? null : ToSnapshot(existing)
                });
            }

            if (synchronization.ChangeEpoch > scope.ChangeEpoch)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(synchronization),
                    synchronization.ChangeEpoch,
                    $"Catalog change epoch {synchronization.ChangeEpoch} has not been committed.");
            }

            var isSuspended = synchronization.IsSuspended || IsActiveJobDisabledUnsafe(revision);
            if (existing is not null)
            {
                if (existing.Template != synchronization.Template || existing.Schedule != synchronization.Schedule)
                {
                    throw new InvalidOperationException(
                        $"Recurring cursor '{revision.JobRevisionId}' was synchronized with a different execution template.");
                }

                if (existing.LastSynchronizedChangeEpoch == synchronization.ChangeEpoch)
                {
                    return Task.FromResult(new RecurringScheduleSynchronizationResult
                    {
                        Status = RecurringScheduleSynchronizationStatus.Unchanged,
                        Cursor = ToSnapshot(existing)
                    });
                }

                var now = UtcNow;
                if (existing.IsSuspended != isSuspended)
                {
                    existing.IsSuspended = isSuspended;
                    existing.NextOccurrenceUtc = isSuspended
                        ? null
                        : synchronization.Schedule.GetNextOccurrence(now);
                }

                existing.LastSynchronizedChangeEpoch = synchronization.ChangeEpoch;
                existing.Version++;
                existing.UpdatedAtUtc = now;
                return Task.FromResult(new RecurringScheduleSynchronizationResult
                {
                    Status = RecurringScheduleSynchronizationStatus.Updated,
                    Cursor = ToSnapshot(existing)
                });
            }

            var createdAtUtc = UtcNow;
            // Activation, rather than this replica's first synchronization time, is the durable admission boundary.
            // A later resume follows the separate branch above and intentionally starts from the current store time.
            var activation = scope.Activations.SingleOrDefault(item =>
                item.ActivationEpoch == revision.ActivationEpoch)
                ?? throw new InvalidOperationException(
                    $"Catalog activation '{revision.ActivationEpoch}' was not found in scope " +
                    $"'{revision.SchedulerScopeKey}'.");
            var stored = new StoredRecurringCursor
            {
                Template = synchronization.Template,
                Schedule = synchronization.Schedule,
                NextOccurrenceUtc = isSuspended
                    ? null
                    : synchronization.Schedule.GetNextOccurrence(activation.ActivatedAtUtc),
                LastSynchronizedChangeEpoch = synchronization.ChangeEpoch,
                IsSuspended = isSuspended,
                Version = 1,
                UpdatedAtUtc = createdAtUtc
            };
            _recurringCursors.Add(key, stored);
            return Task.FromResult(new RecurringScheduleSynchronizationResult
            {
                Status = RecurringScheduleSynchronizationStatus.Created,
                Cursor = ToSnapshot(stored)
            });
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RecurringScheduleCursor>> GetDueRecurringSchedulesAsync(
        string schedulerScopeKey,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Result count must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = UtcNow;
            IReadOnlyList<RecurringScheduleCursor> result = _recurringCursors.Values
                .Where(cursor =>
                    string.Equals(
                        cursor.Template.Revision.SchedulerScopeKey,
                        schedulerScopeKey,
                        StringComparison.Ordinal)
                    && !cursor.IsSuspended
                    && cursor.NextOccurrenceUtc <= now
                    && IsStableActivation(
                        schedulerScopeKey,
                        cursor.Template.Revision.ActivationEpoch))
                .OrderBy(cursor => cursor.NextOccurrenceUtc)
                .ThenBy(cursor => cursor.Template.Revision.JobRevisionId, StringComparer.Ordinal)
                .Take(maxCount)
                .Select(ToSnapshot)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<RecurringMaterializationResult> TryMaterializeRecurringOccurrenceAsync(
        RecurringOccurrenceMaterialization materialization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(materialization);
        materialization.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var expectedOccurrence = NormalizeUtc(materialization.ExpectedOccurrenceUtc);
            var cursor = GetRecurringCursorUnsafe(materialization.CursorKey);
            if (cursor is null)
            {
                return Task.FromResult(new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.CursorNotFound
                });
            }

            if (!IsStableActivation(
                    materialization.CursorKey.SchedulerScopeKey,
                    materialization.CursorKey.ActivationEpoch))
            {
                return Task.FromResult(new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.InactiveActivation,
                    Cursor = ToSnapshot(cursor)
                });
            }

            if (cursor.IsSuspended || IsActiveJobDisabledUnsafe(cursor.Template.Revision))
            {
                SuspendRecurringCursorUnsafe(cursor);
                return Task.FromResult(new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.Suspended,
                    Cursor = ToSnapshot(cursor)
                });
            }

            if (cursor.Version != materialization.ExpectedVersion
                || cursor.NextOccurrenceUtc != expectedOccurrence)
            {
                return Task.FromResult(new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.StaleCursor,
                    Cursor = ToSnapshot(cursor)
                });
            }

            var now = UtcNow;
            if (expectedOccurrence > now)
            {
                return Task.FromResult(new RecurringMaterializationResult
                {
                    Status = RecurringMaterializationStatus.NotDue,
                    Cursor = ToSnapshot(cursor)
                });
            }

            var execution = EnqueueCapturedUnsafe(
                new JobEnqueueRequest
                {
                    InstanceId = materialization.InstanceId,
                    SchedulerScopeKey = cursor.Template.Revision.SchedulerScopeKey,
                    JobKey = cursor.Template.Revision.JobKey,
                    AvailableAtUtc = expectedOccurrence,
                    EnqueueReason = $"Recurring occurrence {expectedOccurrence:O} materialized"
                },
                cursor.Template,
                now);
            cursor.NextOccurrenceUtc = materialization.NextOccurrenceUtc is { } next ? NormalizeUtc(next) : null;
            cursor.Version++;
            cursor.UpdatedAtUtc = now;

            return Task.FromResult(new RecurringMaterializationResult
            {
                Status = RecurringMaterializationStatus.Materialized,
                Execution = execution,
                Cursor = ToSnapshot(cursor)
            });
        }
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    private JobExecutionTemplate ResolveActiveExecutionTemplateUnsafe(JobEnqueueRequest request)
    {
        if (!_catalogScopes.TryGetValue(request.SchedulerScopeKey, out var scope)
            || scope.ActiveReleaseId is null)
        {
            throw new JobCatalogNotFoundException(
                $"Scheduler scope '{request.SchedulerScopeKey}' has no active catalog.");
        }

        if (scope.ActiveIntentEpoch != scope.DesiredIntentEpoch)
        {
            throw new JobCatalogTransitionException(request.SchedulerScopeKey);
        }

        var definition = ProjectActiveDefinitions(request.SchedulerScopeKey, scope)
            .SingleOrDefault(candidate => string.Equals(
                candidate.Declaration.JobKey,
                request.JobKey,
                StringComparison.Ordinal))
            ?? throw new JobCatalogNotFoundException(
                $"Active job '{request.JobKey}' was not found in scope '{request.SchedulerScopeKey}'.");
        if (definition.IsDisabled)
        {
            throw new InvalidOperationException($"Active job '{request.JobKey}' is disabled.");
        }

        var ownerMismatch = request.ExpectedOwnerId is not null
                            && !string.Equals(request.ExpectedOwnerId, definition.OwnerId, StringComparison.Ordinal);
        var revisionMismatch = request.ExpectedJobRevisionId is not null
                               && !string.Equals(
                                   request.ExpectedJobRevisionId,
                                   definition.JobRevisionId,
                                   StringComparison.Ordinal);
        if (ownerMismatch || revisionMismatch)
        {
            throw new JobRevisionMismatchException(request.JobKey);
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

    private static JobExecutionInstance ResolveIdempotentEnqueueUnsafe(
        JobEnqueueRequest request,
        StoredExecution existing)
    {
        var revision = existing.Template.Revision;
        var jobMatches = string.Equals(request.JobKey, revision.JobKey, StringComparison.Ordinal);
        var ownerMatches = request.ExpectedOwnerId is null
                           || string.Equals(request.ExpectedOwnerId, revision.OwnerKey, StringComparison.Ordinal);
        var revisionMatches = request.ExpectedJobRevisionId is null
                              || string.Equals(
                                  request.ExpectedJobRevisionId,
                                  revision.JobRevisionId,
                                  StringComparison.Ordinal);
        var payloadMatches = string.Equals(existing.JobArgs, request.JobArgs, StringComparison.Ordinal);
        var availabilityMatches = request.AvailableAtUtc is not { } requestedAvailability
                                  || existing.AvailableAtUtc == NormalizeUtc(requestedAvailability);
        if (!jobMatches || !ownerMatches || !revisionMatches || !payloadMatches || !availabilityMatches)
        {
            throw new InvalidOperationException(
                $"Execution identifier '{request.InstanceId}' was reused for a different enqueue request.");
        }

        return ToSummary(existing);
    }

    private JobExecutionInstance EnqueueCapturedUnsafe(
        JobEnqueueRequest request,
        JobExecutionTemplate template,
        DateTimeOffset now)
    {
        var normalizedAvailableAt = request.AvailableAtUtc is { } requestedAvailability
            ? NormalizeUtc(requestedAvailability)
            : now;
        var key = new ExecutionKey(template.Revision.SchedulerScopeKey, request.InstanceId);
        if (_executionInstances.TryGetValue(key, out var existing))
        {
            if (existing.Template != template
                || !string.Equals(existing.JobArgs, request.JobArgs, StringComparison.Ordinal)
                || (request.AvailableAtUtc is not null
                    && existing.AvailableAtUtc != normalizedAvailableAt))
            {
                throw new InvalidOperationException(
                    $"Execution identifier '{request.InstanceId}' was reused for a different enqueue request.");
            }

            return ToSummary(existing);
        }

        var execution = new StoredExecution
        {
            InstanceId = request.InstanceId,
            Template = template,
            JobArgs = request.JobArgs,
            AvailableAtUtc = normalizedAvailableAt,
            State = JobExecutionState.Queued,
            CreatedAtUtc = now
        };
        AddHistory(execution, new JobExecutionHistoryEntry
        {
            TimestampUtc = now,
            Kind = JobExecutionHistoryKind.StateTransition,
            NewState = JobExecutionState.Queued,
            Message = request.EnqueueReason
        });
        _executionInstances.Add(key, execution);
        return ToSummary(execution);
    }

    private JobAttemptCompletionResult CompleteAttemptUnsafe(
        JobAttemptCompletion completion,
        DateTimeOffset now)
    {
        if (!TryGetCurrentExecutionLeaseUnsafe(completion.LeaseKey, now, out var execution))
        {
            return new JobAttemptCompletionResult { Status = JobAttemptCompletionStatus.Lost };
        }

        var workerInstanceId = execution.RunningWorkerInstanceId;
        ReleaseExecutionGateUnsafe(execution);
        var cancellationWins = execution.CancellationRequestedAtUtc.HasValue;
        var outcome = cancellationWins ? JobAttemptOutcome.Cancelled : completion.Outcome;
        var message = completion.Message;

        switch (outcome)
        {
            case JobAttemptOutcome.Succeeded:
                execution.State = JobExecutionState.Succeeded;
                execution.CompletedAtUtc = now;
                AddHistory(execution, Transition(
                    now,
                    JobExecutionState.Running,
                    JobExecutionState.Succeeded,
                    message ?? "Execution completed successfully",
                    workerInstanceId));
                break;

            case JobAttemptOutcome.Cancelled:
                execution.State = JobExecutionState.Cancelled;
                execution.CompletedAtUtc = now;
                AddHistory(execution, Transition(
                    now,
                    JobExecutionState.Running,
                    JobExecutionState.Cancelled,
                    message ?? (cancellationWins
                        ? "Execution cancelled by persisted request"
                        : "Execution observed cancellation"),
                    workerInstanceId));
                break;

            case JobAttemptOutcome.Abandoned:
                execution.State = JobExecutionState.Queued;
                execution.AvailableAtUtc = now;
                AddHistory(execution, Transition(
                    now,
                    JobExecutionState.Running,
                    JobExecutionState.Queued,
                    message ?? "Execution released for another worker",
                    workerInstanceId));
                break;

            case JobAttemptOutcome.Failed:
                execution.RetryAttempt++;
                if (execution.RetryAttempt <= execution.Template.RetryCount)
                {
                    execution.State = JobExecutionState.Queued;
                    execution.AvailableAtUtc = now.Add(completion.RetryDelay);
                    AddHistory(execution, Transition(
                        now,
                        JobExecutionState.Running,
                        JobExecutionState.Queued,
                        message ?? $"Execution failed; retry {execution.RetryAttempt} queued",
                        workerInstanceId,
                        LogLevel.Warning));
                }
                else
                {
                    execution.State = JobExecutionState.Failed;
                    execution.CompletedAtUtc = now;
                    AddHistory(execution, Transition(
                        now,
                        JobExecutionState.Running,
                        JobExecutionState.Failed,
                        message ?? "Execution failed and exhausted its retries",
                        workerInstanceId,
                        LogLevel.Error));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(completion), completion.Outcome, "Unsupported outcome.");
        }

        ClearExecutionLeaseUnsafe(execution);
        return new JobAttemptCompletionResult
        {
            Status = JobAttemptCompletionStatus.Applied,
            Execution = ToSummary(execution)
        };
    }

    private bool TryGetCurrentCapabilityUnsafe(
        WorkerCapabilityLeaseKey leaseKey,
        DateTimeOffset now,
        out StoredWorkerCapability capability)
    {
        var key = new WorkerCapabilityKey(leaseKey.SchedulerScopeKey, leaseKey.WorkerInstanceId);
        return _workerCapabilityLeases.TryGetValue(key, out capability!)
               && capability.LeaseExpiresAtUtc > now
               && string.Equals(capability.LeaseToken, leaseKey.LeaseToken, StringComparison.Ordinal);
    }

    private bool TryGetCurrentExecutionLeaseUnsafe(
        JobLeaseKey leaseKey,
        DateTimeOffset now,
        out StoredExecution execution)
    {
        var key = new ExecutionKey(leaseKey.SchedulerScopeKey, leaseKey.InstanceId);
        if (!_executionInstances.TryGetValue(key, out execution!)
            || execution.State != JobExecutionState.Running
            || execution.LeaseExpiresAtUtc <= now
            || !string.Equals(execution.RunningWorkerInstanceId, leaseKey.WorkerInstanceId, StringComparison.Ordinal)
            || !string.Equals(execution.ExecutionLeaseToken, leaseKey.LeaseToken, StringComparison.Ordinal))
        {
            return false;
        }

        var capabilityKey = new WorkerCapabilityKey(leaseKey.SchedulerScopeKey, leaseKey.WorkerInstanceId);
        return _workerCapabilityLeases.TryGetValue(capabilityKey, out var capability)
               && capability.LeaseExpiresAtUtc > now
               && string.Equals(capability.LeaseToken, execution.CapabilityLeaseToken, StringComparison.Ordinal);
    }

    private static bool IsCompatible(StoredExecution execution, StoredWorkerCapability capability)
    {
        var revision = execution.Template.Revision;
        return string.Equals(revision.SchedulerScopeKey, capability.SchedulerScopeKey, StringComparison.Ordinal)
               && string.Equals(revision.OwnerKey, capability.OwnerKey, StringComparison.Ordinal)
               && string.Equals(revision.WorkerRevisionId, capability.WorkerRevisionId, StringComparison.Ordinal)
               && capability.JobRevisionIds.Contains(revision.JobRevisionId);
    }

    private void ReleaseExecutionGateUnsafe(StoredExecution execution)
    {
        var gateKey = GetExecutionGateKey(execution.Template.Revision);
        var activeCount = _executionGateCounts.GetValueOrDefault(gateKey);
        if (activeCount < 1)
        {
            throw new InvalidOperationException(
                $"Execution gate '{execution.Template.Revision.JobKey}' has no active lease to release.");
        }

        if (activeCount == 1)
        {
            _executionGateCounts.Remove(gateKey);
        }
        else
        {
            _executionGateCounts[gateKey] = activeCount - 1;
        }
    }

    private static void ClearExecutionLeaseUnsafe(StoredExecution execution)
    {
        execution.RunningWorkerInstanceId = null;
        execution.ExecutionLeaseToken = null;
        execution.CapabilityLeaseToken = null;
        execution.LeaseExpiresAtUtc = null;
        if (execution.State == JobExecutionState.Queued)
        {
            execution.CancellationRequestedAtUtc = null;
        }
    }

    private StoredRecurringCursor? GetRecurringCursorUnsafe(RecurringScheduleCursorKey key)
    {
        return _recurringCursors.GetValueOrDefault(new RecurringCursorStorageKey(
            key.SchedulerScopeKey,
            key.ActivationEpoch,
            key.JobRevisionId));
    }

    private bool IsActiveJobDisabledUnsafe(JobRevisionIdentity revision)
    {
        var scope = _catalogScopes[revision.SchedulerScopeKey];
        var release = GetRelease(scope, scope.ActiveReleaseId!);
        if (!release.Owners.TryGetValue(revision.OwnerKey, out var owner)
            || !string.Equals(
                owner.Manifest.WorkerRevisionId,
                revision.WorkerRevisionId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Active catalog no longer contains worker revision '{revision.OwnerKey}/{revision.WorkerRevisionId}'.");
        }

        var declaration = owner.Snapshot!.Declarations.SingleOrDefault(candidate =>
            string.Equals(candidate.JobKey, revision.JobKey, StringComparison.Ordinal)
            && string.Equals(
                JobCatalogHash.ComputeJobRevision(owner.Manifest.OwnerId, owner.Manifest.WorkerRevisionId, candidate),
                revision.JobRevisionId,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Active catalog no longer contains job revision '{revision.JobRevisionId}'.");
        var policy = scope.Policies[declaration.JobKey];
        return policy.DisabledOverride ?? declaration.IsDisabledByDefault;
    }

    private void SuspendRecurringCursorUnsafe(StoredRecurringCursor cursor)
    {
        var changeEpoch = _catalogScopes[cursor.Template.Revision.SchedulerScopeKey].ChangeEpoch;
        if (cursor.IsSuspended
            && cursor.NextOccurrenceUtc is null
            && cursor.LastSynchronizedChangeEpoch >= changeEpoch)
        {
            return;
        }

        cursor.IsSuspended = true;
        cursor.NextOccurrenceUtc = null;
        cursor.LastSynchronizedChangeEpoch = Math.Max(cursor.LastSynchronizedChangeEpoch, changeEpoch);
        cursor.Version++;
        cursor.UpdatedAtUtc = UtcNow;
    }

    private void AddHistory(StoredExecution execution, JobExecutionHistoryEntry entry)
    {
        execution.History.Add(entry with { Message = _historyLimits.NormalizeMessage(entry.Message) });
        var excessCount = execution.History.Count - _historyLimits.MaxEntries;
        if (excessCount > 0)
        {
            execution.History.RemoveRange(0, excessCount);
        }
    }

    private RetirementCounts RetireSupersededExecutionsUnsafe(
        string schedulerScopeKey,
        long nextActivationEpoch,
        DateTimeOffset activatedAtUtc)
    {
        // Keep volatile semantics aligned with the relational aggregate cutover: fields and activation counts are
        // authoritative, and activation does not fan out one history entry per affected execution.
        var queuedCount = 0;
        var runningCount = 0;
        foreach (var execution in _executionInstances.Values.Where(execution =>
                     string.Equals(
                         execution.Template.Revision.SchedulerScopeKey,
                         schedulerScopeKey,
                         StringComparison.Ordinal)
                     && execution.Template.Revision.ActivationEpoch < nextActivationEpoch))
        {
            if (execution.State == JobExecutionState.Queued)
            {
                execution.State = JobExecutionState.Cancelled;
                execution.CompletedAtUtc = activatedAtUtc;
                queuedCount++;
            }
            else if (execution.State == JobExecutionState.Running
                     && !execution.CancellationRequestedAtUtc.HasValue)
            {
                execution.CancellationRequestedAtUtc = activatedAtUtc;
                runningCount++;
            }
        }

        return new RetirementCounts(queuedCount, runningCount);
    }

    private void PruneSupersededRecurringCursorsUnsafe(
        string schedulerScopeKey,
        long nextActivationEpoch)
    {
        var obsoleteKeys = _recurringCursors.Keys
            .Where(key => string.Equals(key.SchedulerScopeKey, schedulerScopeKey, StringComparison.Ordinal)
                          && key.ActivationEpoch < nextActivationEpoch)
            .ToArray();
        foreach (var key in obsoleteKeys)
        {
            _recurringCursors.Remove(key);
        }
    }

    private static IOrderedEnumerable<StoredExecution> OrderExecutions(
        IEnumerable<StoredExecution> executions,
        JobExecutionSortField sortField,
        bool descending)
    {
        Func<StoredExecution, DateTimeOffset?> selector = sortField switch
        {
            JobExecutionSortField.CreatedAtUtc => execution => execution.CreatedAtUtc,
            JobExecutionSortField.AvailableAtUtc => execution => execution.AvailableAtUtc,
            JobExecutionSortField.StartedAtUtc => execution => execution.StartedAtUtc,
            JobExecutionSortField.CompletedAtUtc => execution => execution.CompletedAtUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(sortField), sortField, "Unsupported sort field.")
        };
        return descending
            ? executions.OrderByDescending(selector).ThenByDescending(execution => execution.InstanceId, StringComparer.Ordinal)
            : executions.OrderBy(selector).ThenBy(execution => execution.InstanceId, StringComparer.Ordinal);
    }

    private static JobExecutionHistoryEntry Transition(
        DateTimeOffset timestampUtc,
        JobExecutionState previousState,
        JobExecutionState newState,
        string message,
        string? workerInstanceId,
        LogLevel logLevel = LogLevel.Information)
    {
        return new JobExecutionHistoryEntry
        {
            TimestampUtc = timestampUtc,
            Kind = JobExecutionHistoryKind.StateTransition,
            PreviousState = previousState,
            NewState = newState,
            LogLevel = logLevel,
            Message = message,
            WorkerInstanceId = workerInstanceId
        };
    }

    private static JobExecutionInstance ToSnapshot(StoredExecution execution) =>
        CreateSnapshot(execution, true);

    private static JobExecutionInstance ToSummary(StoredExecution execution) =>
        CreateSnapshot(execution, false);

    private static JobExecutionInstance CreateSnapshot(StoredExecution execution, bool includeHistory)
    {
        return new JobExecutionInstance
        {
            InstanceId = execution.InstanceId,
            Template = execution.Template,
            JobArgs = execution.JobArgs,
            AvailableAtUtc = execution.AvailableAtUtc,
            State = execution.State,
            CreatedAtUtc = execution.CreatedAtUtc,
            StartedAtUtc = execution.StartedAtUtc,
            CompletedAtUtc = execution.CompletedAtUtc,
            ExecutionAttempt = execution.ExecutionAttempt,
            RetryAttempt = execution.RetryAttempt,
            LeaseLossCount = execution.LeaseLossCount,
            RunningWorkerInstanceId = execution.RunningWorkerInstanceId,
            LeaseExpiresAtUtc = execution.LeaseExpiresAtUtc,
            CancellationRequestedAtUtc = execution.CancellationRequestedAtUtc,
            History = includeHistory ? execution.History.ToArray() : []
        };
    }

    private static WorkerCapabilityLease ToSnapshot(StoredWorkerCapability capability)
    {
        return new WorkerCapabilityLease
        {
            Capability = new WorkerCapabilityRegistration
            {
                SchedulerScopeKey = capability.SchedulerScopeKey,
                OwnerKey = capability.OwnerKey,
                WorkerRevisionId = capability.WorkerRevisionId,
                WorkerInstanceId = capability.WorkerInstanceId,
                JobRevisionIds = capability.JobRevisionIds.Order(StringComparer.Ordinal).ToArray()
            },
            LeaseKey = new WorkerCapabilityLeaseKey
            {
                SchedulerScopeKey = capability.SchedulerScopeKey,
                WorkerInstanceId = capability.WorkerInstanceId,
                LeaseToken = capability.LeaseToken
            },
            LeaseExpiresAtUtc = capability.LeaseExpiresAtUtc
        };
    }

    private static RecurringScheduleCursor ToSnapshot(StoredRecurringCursor cursor)
    {
        var revision = cursor.Template.Revision;
        return new RecurringScheduleCursor
        {
            Key = new RecurringScheduleCursorKey
            {
                SchedulerScopeKey = revision.SchedulerScopeKey,
                ActivationEpoch = revision.ActivationEpoch,
                JobRevisionId = revision.JobRevisionId
            },
            Template = cursor.Template,
            Schedule = cursor.Schedule,
            NextOccurrenceUtc = cursor.NextOccurrenceUtc,
            LastSynchronizedChangeEpoch = cursor.LastSynchronizedChangeEpoch,
            IsSuspended = cursor.IsSuspended,
            Version = cursor.Version,
            UpdatedAtUtc = cursor.UpdatedAtUtc
        };
    }

    private static bool IsTerminal(JobExecutionState state)
    {
        return state is JobExecutionState.Succeeded or JobExecutionState.Failed or JobExecutionState.Cancelled;
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value) => value.ToUniversalTime();

    private static void ValidatePositiveDuration(TimeSpan duration, string parameterName)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, duration, "Duration must be greater than zero.");
        }
    }

    private static ExecutionGateKey GetExecutionGateKey(JobRevisionIdentity revision)
    {
        return new ExecutionGateKey(revision.SchedulerScopeKey, revision.JobKey);
    }

    private readonly record struct ExecutionKey(string SchedulerScopeKey, string InstanceId);
    private readonly record struct ExecutionGateKey(string SchedulerScopeKey, string JobKey);
    private readonly record struct WorkerCapabilityKey(string SchedulerScopeKey, string WorkerInstanceId);
    private readonly record struct RecurringCursorStorageKey(
        string SchedulerScopeKey,
        long ActivationEpoch,
        string JobRevisionId);
    private readonly record struct RetirementCounts(int QueuedCount, int RunningCount);

    private sealed class StoredExecution
    {
        public required string InstanceId { get; init; }
        public required JobExecutionTemplate Template { get; init; }
        public string? JobArgs { get; init; }
        public DateTimeOffset AvailableAtUtc { get; set; }
        public JobExecutionState State { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public int ExecutionAttempt { get; set; }
        public int RetryAttempt { get; set; }
        public int LeaseLossCount { get; set; }
        public string? RunningWorkerInstanceId { get; set; }
        public string? ExecutionLeaseToken { get; set; }
        public string? CapabilityLeaseToken { get; set; }
        public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
        public DateTimeOffset? CancellationRequestedAtUtc { get; set; }
        public List<JobExecutionHistoryEntry> History { get; } = [];
    }

    private sealed class StoredWorkerCapability
    {
        public required string SchedulerScopeKey { get; init; }
        public required string OwnerKey { get; init; }
        public required string WorkerRevisionId { get; init; }
        public required string WorkerInstanceId { get; init; }
        public required HashSet<string> JobRevisionIds { get; init; }
        public required string LeaseToken { get; init; }
        public DateTimeOffset LeaseExpiresAtUtc { get; set; }
    }

    private sealed class StoredRecurringCursor
    {
        public required JobExecutionTemplate Template { get; init; }
        public required RecurringScheduleDefinition Schedule { get; init; }
        public DateTimeOffset? NextOccurrenceUtc { get; set; }
        public long LastSynchronizedChangeEpoch { get; set; }
        public bool IsSuspended { get; set; }
        public long Version { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
