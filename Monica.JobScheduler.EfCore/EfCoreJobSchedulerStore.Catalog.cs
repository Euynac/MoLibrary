using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Utils;

namespace Monica.JobScheduler.EfCore;

public sealed partial class EfCoreJobSchedulerStore
{
    /// <inheritdoc />
    public Task<ReleaseStageResult> StageReleaseAsync(
        JobCatalogReleaseStage stage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(stage.Manifest);
        if (stage.DeploymentGeneration < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage.DeploymentGeneration,
                "Deployment generation must be greater than zero.");
        }
        var normalized = NormalizeManifest(stage.Manifest);
        return WriteAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == normalized.SchedulerScopeKey,
                token);
            if (scope is null)
            {
                scope = new JobCatalogScopeEntity
                {
                    SchedulerScopeKey = normalized.SchedulerScopeKey,
                    ConcurrencyToken = NewVersion()
                };
                dbContext.CatalogScopes.Add(scope);
            }
            if (stage.DeploymentGeneration == scope.LastSeenDeploymentGeneration
                && scope.LastSeenDeploymentReleaseId is not null
                && !string.Equals(scope.LastSeenDeploymentReleaseId, normalized.ReleaseId, StringComparison.Ordinal))
            {
                throw new JobCatalogConflictException(
                    $"Deployment generation '{stage.DeploymentGeneration}' already selected release " +
                    $"'{scope.LastSeenDeploymentReleaseId}'.");
            }

            var existing = await dbContext.CatalogReleases.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == normalized.SchedulerScopeKey
                        && item.ReleaseId == normalized.ReleaseId,
                token);
            if (existing is not null)
            {
                if (!string.Equals(existing.ManifestContentHash, normalized.ContentHash, StringComparison.Ordinal))
                {
                    throw new JobCatalogConflictException(
                        $"Release '{normalized.ReleaseId}' already exists with a different owner manifest.");
                }

                if (stage.DeploymentGeneration > scope.LastSeenDeploymentGeneration)
                {
                    scope.LastSeenDeploymentGeneration = stage.DeploymentGeneration;
                    scope.LastSeenDeploymentReleaseId = normalized.ReleaseId;
                    scope.DesiredReleaseId = normalized.ReleaseId;
                    scope.DesiredIntentEpoch++;
                    scope.PublicationEpoch++;
                    scope.ConcurrencyToken = NewVersion();
                    return new ReleaseStageResult(
                        ReleaseStageStatus.StagedAsDesired,
                        normalized.ReleaseId,
                        normalized.ContentHash,
                        ToVersion(scope));
                }

                return new ReleaseStageResult(
                    ReleaseStageStatus.Duplicate,
                    normalized.ReleaseId,
                    normalized.ContentHash,
                    ToVersion(scope));
            }

            scope.PublicationEpoch++;
            var status = ReleaseStageStatus.StagedAsSuperseded;
            if (stage.DeploymentGeneration > scope.LastSeenDeploymentGeneration)
            {
                scope.DesiredReleaseId = normalized.ReleaseId;
                scope.LastSeenDeploymentGeneration = stage.DeploymentGeneration;
                scope.LastSeenDeploymentReleaseId = normalized.ReleaseId;
                scope.DesiredIntentEpoch++;
                status = ReleaseStageStatus.StagedAsDesired;
            }
            scope.ConcurrencyToken = NewVersion();

            dbContext.CatalogReleases.Add(new JobCatalogReleaseEntity
            {
                SchedulerScopeKey = normalized.SchedulerScopeKey,
                ReleaseId = normalized.ReleaseId,
                ManifestContentHash = normalized.ContentHash,
                PayloadJson = Serialize(new CatalogReleasePayload
                {
                    Manifest = normalized,
                    OwnerSnapshots = normalized.Owners.ToDictionary(
                        static owner => owner.OwnerId,
                        static _ => (JobOwnerCatalogSnapshot?)null,
                        StringComparer.Ordinal)
                }),
                ConcurrencyToken = NewVersion()
            });
            return new ReleaseStageResult(status, normalized.ReleaseId, normalized.ContentHash, ToVersion(scope));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OwnerSnapshotPublishResult> PublishOwnerSnapshotAsync(
        JobOwnerCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalized = NormalizeSnapshot(snapshot);
        return WriteAsync(async (dbContext, token) =>
        {
            var release = await GetReleaseAsync(dbContext, normalized.SchedulerScopeKey, normalized.ReleaseId, token);
            var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
            var owner = payload.Manifest.Owners.SingleOrDefault(item =>
                string.Equals(item.OwnerId, normalized.OwnerId, StringComparison.Ordinal));
            if (owner is null)
            {
                throw new JobCatalogConflictException(
                    $"Owner '{normalized.OwnerId}' is not expected by release '{normalized.ReleaseId}'.");
            }

            if (!string.Equals(owner.WorkerRevisionId, normalized.WorkerRevisionId, StringComparison.Ordinal))
            {
                throw new JobCatalogConflictException(
                    $"Owner '{normalized.OwnerId}' published worker revision '{normalized.WorkerRevisionId}', " +
                    $"but release '{normalized.ReleaseId}' expects '{owner.WorkerRevisionId}'.");
            }

            if (payload.OwnerSnapshots[normalized.OwnerId] is { } existing)
            {
                if (!string.Equals(existing.ContentHash, normalized.ContentHash, StringComparison.Ordinal))
                {
                    throw new JobCatalogConflictException(
                        $"Owner '{normalized.OwnerId}' already published different declarations for release " +
                        $"'{normalized.ReleaseId}'.");
                }

                return new OwnerSnapshotPublishResult(
                    OwnerSnapshotPublishStatus.Duplicate,
                    normalized.ReleaseId,
                    normalized.OwnerId,
                    normalized.ContentHash);
            }

            var publishedKeys = payload.OwnerSnapshots.Values
                .OfType<JobOwnerCatalogSnapshot>()
                .SelectMany(static item => item.Declarations)
                .Select(static declaration => declaration.JobKey)
                .ToHashSet(StringComparer.Ordinal);
            var collision = normalized.Declarations.FirstOrDefault(item => publishedKeys.Contains(item.JobKey));
            if (collision is not null)
            {
                throw new JobCatalogConflictException(
                    $"Job key '{collision.JobKey}' is declared by more than one owner in release '{normalized.ReleaseId}'.");
            }

            payload.OwnerSnapshots[normalized.OwnerId] = normalized;
            release.PayloadJson = Serialize(payload);
            release.ConcurrencyToken = NewVersion();
            var scope = await GetScopeAsync(dbContext, normalized.SchedulerScopeKey, token);
            scope.PublicationEpoch++;
            scope.ConcurrencyToken = NewVersion();
            return new OwnerSnapshotPublishResult(
                OwnerSnapshotPublishStatus.Published,
                normalized.ReleaseId,
                normalized.OwnerId,
                normalized.ContentHash);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCatalogActivationResult> TryActivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(releaseId, nameof(releaseId));
        return WriteAsync(async (dbContext, token) =>
        {
            var scope = await GetScopeAsync(dbContext, schedulerScopeKey, token);
            var release = await GetReleaseAsync(dbContext, schedulerScopeKey, releaseId, token);
            var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
            if (!string.Equals(scope.DesiredReleaseId, releaseId, StringComparison.Ordinal))
            {
                return new JobCatalogActivationResult(
                    JobCatalogActivationStatus.NotDesired,
                    ToVersion(scope),
                    []);
            }

            var missingOwners = GetMissingOwners(payload);
            if (missingOwners.Length != 0)
            {
                return new JobCatalogActivationResult(
                    JobCatalogActivationStatus.NotReady,
                    ToVersion(scope),
                    missingOwners);
            }

            if (scope.ActiveIntentEpoch == scope.DesiredIntentEpoch)
            {
                return new JobCatalogActivationResult(
                    JobCatalogActivationStatus.AlreadyActive,
                    ToVersion(scope),
                    []);
            }

            var activation = await ActivateAsync(dbContext, scope, release, payload, false, token);
            return new JobCatalogActivationResult(
                JobCatalogActivationStatus.Activated,
                ToVersion(scope),
                [],
                activation);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCatalogActivation> ReactivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(releaseId, nameof(releaseId));
        return WriteAsync(async (dbContext, token) =>
        {
            var scope = await GetScopeAsync(dbContext, schedulerScopeKey, token);
            var release = await GetReleaseAsync(dbContext, schedulerScopeKey, releaseId, token);
            var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
            var missingOwners = GetMissingOwners(payload);
            if (missingOwners.Length != 0)
            {
                throw new JobCatalogConflictException(
                    $"Release '{releaseId}' cannot be reactivated before owners [{string.Join(", ", missingOwners)}] publish.");
            }

            scope.DesiredReleaseId = releaseId;
            scope.DesiredIntentEpoch++;
            scope.PublicationEpoch++;
            return await ActivateAsync(dbContext, scope, release, payload, true, token);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCatalogVersion> GetCatalogVersionAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            return scope is null
                ? EmptyVersion(schedulerScopeKey)
                : ToVersion(scope);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCatalogPublicationStatus> GetCatalogPublicationStatusAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            if (scope?.DesiredReleaseId is null)
            {
                return new JobCatalogPublicationStatus(EmptyVersion(schedulerScopeKey), null, []);
            }

            var release = await dbContext.CatalogReleases.AsNoTracking().SingleAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey
                        && item.ReleaseId == scope.DesiredReleaseId,
                token);
            var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
            var owners = payload.Manifest.Owners
                .OrderBy(static owner => owner.OwnerId, StringComparer.Ordinal)
                .Select(owner => new JobCatalogOwnerPublicationStatus(
                    owner.OwnerId,
                    owner.WorkerRevisionId,
                    payload.OwnerSnapshots[owner.OwnerId] is not null,
                    payload.OwnerSnapshots[owner.OwnerId]?.ContentHash))
                .ToArray();
            return new JobCatalogPublicationStatus(ToVersion(scope), release.ManifestContentHash, owners);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobCatalogSnapshot?> GetActiveCatalogAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            if (scope?.ActiveReleaseId is null)
            {
                return null;
            }

            var definitions = await ProjectActiveDefinitionsAsync(dbContext, scope, token);
            return new JobCatalogSnapshot(
                ToVersion(scope),
                definitions);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ActiveJobDefinition?> GetActiveDefinitionAsync(
        string schedulerScopeKey,
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var scope = await dbContext.CatalogScopes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey,
                token);
            if (scope?.ActiveReleaseId is null)
            {
                return null;
            }

            return (await ProjectActiveDefinitionsAsync(dbContext, scope, token)).FirstOrDefault(item =>
                string.Equals(item.Declaration.JobKey, jobKey, StringComparison.Ordinal));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryResult<ActiveJobDefinition>> QueryActiveDefinitionsAsync(
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
            IEnumerable<ActiveJobDefinition> filtered = scope?.ActiveReleaseId is null
                ? []
                : await ProjectActiveDefinitionsAsync(dbContext, scope, token);
            filtered = FilterDefinitions(filtered, query);

            var ordered = filtered.ApplyCatalogOrdering(query).ToArray();
            return new QueryResult<ActiveJobDefinition>(
                ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToList(),
                ordered.Length);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobPolicy> UpdatePolicyAsync(
        string schedulerScopeKey,
        string ownerId,
        string jobKey,
        JobPolicyChange change,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(ownerId, nameof(ownerId));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Overrides);
        return WriteAsync(async (dbContext, token) =>
        {
            var scope = await GetScopeAsync(dbContext, schedulerScopeKey, token);
            if (scope.ActiveReleaseId is null)
            {
                throw new JobCatalogNotFoundException($"Active job '{jobKey}' was not found in scope '{schedulerScopeKey}'.");
            }

            var release = await GetReleaseAsync(dbContext, schedulerScopeKey, scope.ActiveReleaseId, token);
            var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
            if (!payload.OwnerSnapshots.TryGetValue(ownerId, out var ownerSnapshot)
                || ownerSnapshot is null)
            {
                throw new JobCatalogNotFoundException(
                    $"Active job '{ownerId}/{jobKey}' was not found in scope '{schedulerScopeKey}'.");
            }
            var declaration = ownerSnapshot.Declarations.SingleOrDefault(item =>
                string.Equals(item.JobKey, jobKey, StringComparison.Ordinal));
            if (declaration is null)
            {
                throw new JobCatalogNotFoundException(
                    $"Active job '{ownerId}/{jobKey}' was not found in scope '{schedulerScopeKey}'.");
            }

            var policy = await dbContext.JobPolicies.SingleAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey
                        && item.JobKey == jobKey,
                token);
            if (!string.Equals(policy.ConcurrencyStamp, change.ExpectedConcurrencyStamp, StringComparison.Ordinal))
            {
                throw new JobPolicyConcurrencyException(jobKey);
            }
            var currentPolicy = ToPolicy(policy);
            var normalizedOverrides = change.Overrides.Normalize();
            normalizedOverrides.Validate(declaration, currentPolicy.Overrides);

            var jobRevisionId = JobCatalogHash.ComputeJobRevision(
                ownerSnapshot.OwnerId,
                ownerSnapshot.WorkerRevisionId,
                declaration);
            var now = await GetUtcNowAsync(dbContext, token);
            policy.OverridesJson = Serialize(normalizedOverrides);
            policy.ConcurrencyStamp = NewToken();
            policy.ReviewedAgainstJobRevisionId = jobRevisionId;
            policy.RecurringScheduleEffectiveFromUtcTicks = ToTicks(
                currentPolicy.ResolveRecurringScheduleEffectiveFromUtc(declaration, normalizedOverrides, now));
            policy.UpdatedAtUtcTicks = ToTicks(now);
            policy.ConcurrencyToken = NewVersion();
            scope.ChangeEpoch++;
            scope.ConcurrencyToken = NewVersion();
            var updatedPolicy = ToPolicy(policy);
            var updatedDefinition = new ActiveJobDefinition
            {
                SchedulerScopeKey = scope.SchedulerScopeKey,
                ReleaseId = release.ReleaseId,
                ActivationEpoch = scope.ActivationEpoch,
                OwnerId = ownerSnapshot.OwnerId,
                WorkerRevisionId = ownerSnapshot.WorkerRevisionId,
                Declaration = declaration,
                Policy = updatedPolicy
            };
            await ApplyPolicyToExecutionGateAsync(dbContext, updatedDefinition, token);
            await ApplyPolicyToRecurringCursorAsync(
                dbContext,
                scope,
                updatedDefinition,
                now,
                token);
            return updatedPolicy;
        }, cancellationToken);
    }

    private static async Task ApplyPolicyToExecutionGateAsync(
        JobSchedulerDbContext dbContext,
        ActiveJobDefinition definition,
        CancellationToken cancellationToken)
    {
        var gate = await dbContext.ExecutionGates.SingleAsync(
            item => item.SchedulerScopeKey == definition.SchedulerScopeKey
                    && item.JobKey == definition.Declaration.JobKey,
            cancellationToken);
        gate.MaxConcurrency = definition.EffectiveConfiguration.MaxConcurrency;
        gate.ConcurrencyToken = NewVersion();
    }

    private async Task ApplyPolicyToRecurringCursorAsync(
        JobSchedulerDbContext dbContext,
        JobCatalogScopeEntity scope,
        ActiveJobDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (definition.Declaration.JobType != JobType.Recurring)
        {
            return;
        }
        var cursor = await dbContext.RecurringCursors.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                    && item.ActivationEpoch == scope.ActivationEpoch
                    && item.JobRevisionId == definition.JobRevisionId,
            cancellationToken);
        if (cursor is null)
        {
            return;
        }

        var effective = definition.EffectiveConfiguration;
        var schedule = effective.Schedule
            ?? throw new InvalidOperationException(
                $"Recurring job '{definition.JobRevisionId}' did not resolve an effective schedule.");
        var previousSchedule = Deserialize<RecurringScheduleDefinition>(cursor.ScheduleJson);
        var previousReasons = cursor.SuspensionReasons;
        var nextReasons = effective.IsDisabled
            ? previousReasons | JobRecurringScheduleSuspensionReason.OperatorPolicy
            : previousReasons & ~JobRecurringScheduleSuspensionReason.OperatorPolicy;
        var scheduleChanged = previousSchedule != schedule;
        var resumed = previousReasons != JobRecurringScheduleSuspensionReason.None
                      && nextReasons == JobRecurringScheduleSuspensionReason.None;
        var template = definition.CreateExecutionTemplate();

        cursor.TemplateJson = Serialize(template);
        cursor.ScheduleJson = Serialize(schedule);
        cursor.AppliedPolicyRevision = definition.Policy.ConcurrencyStamp;
        cursor.SuspensionReasons = nextReasons;
        if (nextReasons != JobRecurringScheduleSuspensionReason.None)
        {
            cursor.NextOccurrenceUtcTicks = null;
        }
        else if (scheduleChanged || resumed)
        {
            // A policy change is prospective. Replacing or resuming a schedule starts strictly after database current
            // time and never replays occurrences suppressed by the previous policy.
            cursor.NextOccurrenceUtcTicks = ToTicks(schedule.GetNextOccurrence(now));
        }

        cursor.LastSynchronizedChangeEpoch = scope.ChangeEpoch;
        cursor.CursorVersion++;
        cursor.UpdatedAtUtcTicks = ToTicks(now);
        cursor.ConcurrencyToken = NewVersion();
    }

    private async Task<JobCatalogActivation> ActivateAsync(
        JobSchedulerDbContext dbContext,
        JobCatalogScopeEntity scope,
        JobCatalogReleaseEntity release,
        CatalogReleasePayload payload,
        bool explicitReactivation,
        CancellationToken cancellationToken)
    {
        var declaredJobs = payload.OwnerSnapshots.Values.OfType<JobOwnerCatalogSnapshot>()
            .SelectMany(snapshot => snapshot.Declarations.Select(declaration => new
            {
                Declaration = declaration,
                declaration.JobKey,
                JobRevisionId = JobCatalogHash.ComputeJobRevision(
                    snapshot.OwnerId,
                    snapshot.WorkerRevisionId,
                    declaration)
            }))
            .ToArray();
        var existingPolicyEntities = await dbContext.JobPolicies
            .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey)
            .ToArrayAsync(cancellationToken);
        var policyEntitiesByJobKey = existingPolicyEntities.ToDictionary(
            static item => item.JobKey,
            StringComparer.Ordinal);
        var previousJobRevisions = await GetActiveJobRevisionMapAsync(
            dbContext,
            scope,
            cancellationToken);
        var policiesByJobKey = new Dictionary<string, JobPolicy>(StringComparer.Ordinal);
        var maxConcurrencyByJobKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var now = await GetUtcNowAsync(dbContext, cancellationToken);
        foreach (var declaredJob in declaredJobs)
        {
            JobPolicy policy;
            if (policyEntitiesByJobKey.TryGetValue(declaredJob.JobKey, out var existingPolicyEntity))
            {
                policy = ToPolicy(existingPolicyEntity);
            }
            else
            {
                policy = new JobPolicy
                {
                    Overrides = new JobPolicyOverrides(),
                    ConcurrencyStamp = NewToken(),
                    ReviewedAgainstJobRevisionId = declaredJob.JobRevisionId,
                    RecurringScheduleEffectiveFromUtc = null,
                    UpdatedAtUtc = now
                };
            }

            try
            {
                policy.Overrides.Validate(declaredJob.Declaration, policy.Overrides);
            }
            catch (ArgumentException exception)
            {
                throw new JobCatalogConflictException(
                    $"Sticky policy for job '{declaredJob.JobKey}' is incompatible with release "
                    + $"'{release.ReleaseId}': {exception.Message}");
            }

            policiesByJobKey.Add(declaredJob.JobKey, policy);
            maxConcurrencyByJobKey.Add(
                declaredJob.JobKey,
                policy.Overrides.Resolve(declaredJob.Declaration).MaxConcurrency);
        }

        // Validation above is intentionally side-effect free. Only after every sticky policy is known to be valid do
        // we persist new policies and rotate fences for incoming code revisions within this serializable cutover.
        foreach (var declaredJob in declaredJobs)
        {
            var policy = policiesByJobKey[declaredJob.JobKey];
            if (policyEntitiesByJobKey.TryGetValue(declaredJob.JobKey, out var existingPolicyEntity))
            {
                if (!previousJobRevisions.TryGetValue(declaredJob.JobKey, out var previousJobRevision)
                    || !string.Equals(previousJobRevision, declaredJob.JobRevisionId, StringComparison.Ordinal))
                {
                    existingPolicyEntity.ConcurrencyStamp = NewToken();
                }

                continue;
            }

            dbContext.JobPolicies.Add(new JobPolicyEntity
            {
                SchedulerScopeKey = scope.SchedulerScopeKey,
                JobKey = declaredJob.JobKey,
                OverridesJson = Serialize(policy.Overrides),
                ConcurrencyStamp = policy.ConcurrencyStamp,
                ReviewedAgainstJobRevisionId = policy.ReviewedAgainstJobRevisionId,
                RecurringScheduleEffectiveFromUtcTicks = ToTicks(policy.RecurringScheduleEffectiveFromUtc),
                UpdatedAtUtcTicks = ToTicks(policy.UpdatedAtUtc),
                ConcurrencyToken = NewVersion()
            });
        }

        var jobKeys = declaredJobs.Select(static item => item.JobKey).ToArray();
        var gatesByJobKey = (await dbContext.ExecutionGates
                .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                               && jobKeys.Contains(item.JobKey))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(static item => item.JobKey, StringComparer.Ordinal);
        foreach (var declaredJob in declaredJobs)
        {
            if (!gatesByJobKey.TryGetValue(declaredJob.JobKey, out var gate))
            {
                gate = new JobExecutionGateEntity
                {
                    SchedulerScopeKey = scope.SchedulerScopeKey,
                    JobKey = declaredJob.JobKey,
                    ConcurrencyToken = NewVersion()
                };
                dbContext.ExecutionGates.Add(gate);
                gatesByJobKey.Add(declaredJob.JobKey, gate);
            }

            gate.MaxConcurrency = maxConcurrencyByJobKey[declaredJob.JobKey];
            gate.ConcurrencyToken = NewVersion();
        }

        var nextActivationEpoch = scope.ActivationEpoch + 1;

        // Activation is an aggregate cutover boundary. Set-based mutations keep its cost independent of backlog
        // materialization while the surrounding serializable transaction preserves exact audit counts. Per-instance
        // cutover history is deliberately omitted because the execution fields and activation counts are authoritative.
        var nowTicks = ToTicks(now);
        var queuedCutoverVersion = NewVersion();
        var retiredQueuedCount = await dbContext.Executions
            .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                           && item.ActivationEpoch < nextActivationEpoch
                           && item.State == JobExecutionState.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, JobExecutionState.Cancelled)
                .SetProperty(item => item.CompletedAtUtcTicks, nowTicks)
                .SetProperty(item => item.ConcurrencyToken, queuedCutoverVersion),
                cancellationToken);
        var runningCutoverVersion = NewVersion();
        var runningCancellationRequestCount = await dbContext.Executions
            .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                           && item.ActivationEpoch < nextActivationEpoch
                           && item.State == JobExecutionState.Running
                           && item.CancellationRequestedAtUtcTicks == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CancellationRequestedAtUtcTicks, nowTicks)
                .SetProperty(item => item.ConcurrencyToken, runningCutoverVersion),
                cancellationToken);

        await dbContext.RecurringCursors
            .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                           && item.ActivationEpoch < nextActivationEpoch)
            .ExecuteDeleteAsync(cancellationToken);

        scope.ActiveReleaseId = release.ReleaseId;
        scope.ActiveIntentEpoch = scope.DesiredIntentEpoch;
        scope.ActivationEpoch = nextActivationEpoch;
        scope.ChangeEpoch++;
        scope.ConcurrencyToken = NewVersion();
        release.ConcurrencyToken = NewVersion();

        var entity = new JobCatalogActivationEntity
        {
            SchedulerScopeKey = scope.SchedulerScopeKey,
            ReleaseId = release.ReleaseId,
            ActivationEpoch = scope.ActivationEpoch,
            DesiredIntentEpoch = scope.DesiredIntentEpoch,
            ActivatedAtUtcTicks = ToTicks(now),
            IsExplicitReactivation = explicitReactivation,
            RetiredQueuedExecutionCount = retiredQueuedCount,
            RunningCancellationRequestCount = runningCancellationRequestCount
        };
        dbContext.CatalogActivations.Add(entity);
        return ToActivation(entity);
    }

    private async Task<Dictionary<string, string>> GetActiveJobRevisionMapAsync(
        JobSchedulerDbContext dbContext,
        JobCatalogScopeEntity scope,
        CancellationToken cancellationToken)
    {
        if (scope.ActiveReleaseId is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var activeRelease = await dbContext.CatalogReleases.AsNoTracking().SingleAsync(
            item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                    && item.ReleaseId == scope.ActiveReleaseId,
            cancellationToken);
        var activePayload = Deserialize<CatalogReleasePayload>(activeRelease.PayloadJson);
        return activePayload.OwnerSnapshots.Values
            .OfType<JobOwnerCatalogSnapshot>()
            .SelectMany(snapshot => snapshot.Declarations.Select(declaration => new
            {
                declaration.JobKey,
                JobRevisionId = JobCatalogHash.ComputeJobRevision(
                    snapshot.OwnerId,
                    snapshot.WorkerRevisionId,
                    declaration)
            }))
            .ToDictionary(static item => item.JobKey, static item => item.JobRevisionId, StringComparer.Ordinal);
    }

    private static async Task<JobCatalogScopeEntity> GetScopeAsync(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey,
        CancellationToken cancellationToken) =>
        await dbContext.CatalogScopes.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == schedulerScopeKey,
            cancellationToken)
        ?? throw new JobCatalogNotFoundException($"Scheduler scope '{schedulerScopeKey}' has no staged releases.");

    private static async Task<JobCatalogReleaseEntity> GetReleaseAsync(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken) =>
        await dbContext.CatalogReleases.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == schedulerScopeKey && item.ReleaseId == releaseId,
            cancellationToken)
        ?? throw new JobCatalogNotFoundException($"Catalog release '{releaseId}' was not found.");

    private async Task<ActiveJobDefinition[]> ProjectActiveDefinitionsAsync(
        JobSchedulerDbContext dbContext,
        JobCatalogScopeEntity scope,
        CancellationToken cancellationToken)
    {
        var release = await dbContext.CatalogReleases.AsNoTracking().SingleAsync(
            item => item.SchedulerScopeKey == scope.SchedulerScopeKey
                    && item.ReleaseId == scope.ActiveReleaseId,
            cancellationToken);
        var payload = Deserialize<CatalogReleasePayload>(release.PayloadJson);
        var policies = await dbContext.JobPolicies.AsNoTracking()
            .Where(item => item.SchedulerScopeKey == scope.SchedulerScopeKey)
            .ToArrayAsync(cancellationToken);
        var policyMap = policies.ToDictionary(static item => item.JobKey, StringComparer.Ordinal);
        return payload.OwnerSnapshots.Values
            .OfType<JobOwnerCatalogSnapshot>()
            .SelectMany(snapshot => snapshot.Declarations.Select(declaration => new ActiveJobDefinition
            {
                SchedulerScopeKey = scope.SchedulerScopeKey,
                ReleaseId = release.ReleaseId,
                ActivationEpoch = scope.ActivationEpoch,
                OwnerId = snapshot.OwnerId,
                WorkerRevisionId = snapshot.WorkerRevisionId,
                Declaration = declaration,
                Policy = ToPolicy(policyMap[declaration.JobKey])
            }))
            .OrderBy(item => item.Declaration.JobKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static JobCatalogVersion ToVersion(JobCatalogScopeEntity entity) =>
        new(
            entity.SchedulerScopeKey,
            entity.ActiveReleaseId,
            entity.DesiredReleaseId,
            entity.LastSeenDeploymentGeneration,
            entity.DesiredIntentEpoch,
            entity.ActiveIntentEpoch,
            entity.ActivationEpoch,
            entity.ChangeEpoch,
            entity.PublicationEpoch);

    private static JobCatalogVersion EmptyVersion(string schedulerScopeKey) =>
        new(schedulerScopeKey, null, null, 0, 0, 0, 0, 0, 0);

    private static JobCatalogActivation ToActivation(JobCatalogActivationEntity entity) =>
        new(
            entity.SchedulerScopeKey,
            entity.ReleaseId,
            entity.ActivationEpoch,
            entity.DesiredIntentEpoch,
            FromTicks(entity.ActivatedAtUtcTicks),
            entity.IsExplicitReactivation,
            entity.RetiredQueuedExecutionCount,
            entity.RunningCancellationRequestCount);

    private static JobPolicy ToPolicy(JobPolicyEntity entity) => new()
    {
        Overrides = Deserialize<JobPolicyOverrides>(entity.OverridesJson).Normalize(),
        ConcurrencyStamp = entity.ConcurrencyStamp,
        ReviewedAgainstJobRevisionId = entity.ReviewedAgainstJobRevisionId,
        RecurringScheduleEffectiveFromUtc = FromTicks(entity.RecurringScheduleEffectiveFromUtcTicks),
        UpdatedAtUtc = FromTicks(entity.UpdatedAtUtcTicks)
    };

    private static string[] GetMissingOwners(CatalogReleasePayload payload) => payload.OwnerSnapshots
        .Where(static item => item.Value is null)
        .Select(static item => item.Key)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static JobCatalogReleaseManifest NormalizeManifest(JobCatalogReleaseManifest manifest)
    {
        ValidateIdentity(manifest.SchedulerScopeKey, nameof(manifest.SchedulerScopeKey));
        ValidateIdentity(manifest.ReleaseId, nameof(manifest.ReleaseId));
        ArgumentNullException.ThrowIfNull(manifest.Owners);
        var owners = manifest.Owners.Select(owner =>
            {
                ArgumentNullException.ThrowIfNull(owner);
                ValidateIdentity(owner.OwnerId, nameof(owner.OwnerId));
                ValidateIdentity(owner.WorkerRevisionId, nameof(owner.WorkerRevisionId));
                return new JobCatalogOwnerManifest(owner.OwnerId, owner.WorkerRevisionId);
            })
            .OrderBy(static owner => owner.OwnerId, StringComparer.Ordinal)
            .ToArray();
        var duplicate = owners.GroupBy(static owner => owner.OwnerId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Owner '{duplicate.Key}' appears more than once in the release manifest.", nameof(manifest));
        }

        return new JobCatalogReleaseManifest(manifest.SchedulerScopeKey, manifest.ReleaseId, owners);
    }

    private static JobOwnerCatalogSnapshot NormalizeSnapshot(JobOwnerCatalogSnapshot snapshot)
    {
        ValidateIdentity(snapshot.SchedulerScopeKey, nameof(snapshot.SchedulerScopeKey));
        ValidateIdentity(snapshot.ReleaseId, nameof(snapshot.ReleaseId));
        ValidateIdentity(snapshot.OwnerId, nameof(snapshot.OwnerId));
        ValidateIdentity(snapshot.WorkerRevisionId, nameof(snapshot.WorkerRevisionId));
        ArgumentNullException.ThrowIfNull(snapshot.Declarations);
        var declarations = snapshot.Declarations
            .Select(static item => item.NormalizeAndValidate())
            .OrderBy(static item => item.JobKey, StringComparer.Ordinal)
            .ToArray();
        var duplicate = declarations.GroupBy(static item => item.JobKey, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Job '{duplicate.Key}' appears more than once in the owner snapshot.", nameof(snapshot));
        }
        return new JobOwnerCatalogSnapshot(
            snapshot.SchedulerScopeKey,
            snapshot.ReleaseId,
            snapshot.OwnerId,
            snapshot.WorkerRevisionId,
            declarations);
    }

}
