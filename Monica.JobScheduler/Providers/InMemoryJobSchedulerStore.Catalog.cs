using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Utils;

namespace Monica.JobScheduler.Providers;

public sealed partial class InMemoryJobSchedulerStore
{
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
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeManifest(stage.Manifest);

        lock (_gate)
        {
            var scope = GetOrCreateScope(normalized.SchedulerScopeKey);
            if (stage.DeploymentGeneration == scope.LastSeenDeploymentGeneration
                && scope.LastSeenDeploymentReleaseId is not null
                && !string.Equals(scope.LastSeenDeploymentReleaseId, normalized.ReleaseId, StringComparison.Ordinal))
            {
                throw new JobCatalogConflictException(
                    $"Deployment generation '{stage.DeploymentGeneration}' already selected release " +
                    $"'{scope.LastSeenDeploymentReleaseId}'.");
            }

            if (scope.Releases.TryGetValue(normalized.ReleaseId, out var existing))
            {
                if (!string.Equals(existing.Manifest.ContentHash, normalized.ContentHash, StringComparison.Ordinal))
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
                    return Task.FromResult(new ReleaseStageResult(
                        ReleaseStageStatus.StagedAsDesired,
                        normalized.ReleaseId,
                        normalized.ContentHash,
                        CreateVersion(normalized.SchedulerScopeKey, scope)));
                }

                return Task.FromResult(new ReleaseStageResult(
                    ReleaseStageStatus.Duplicate,
                    normalized.ReleaseId,
                    normalized.ContentHash,
                    CreateVersion(normalized.SchedulerScopeKey, scope)));
            }

            scope.Releases.Add(normalized.ReleaseId, new CatalogReleaseState(normalized));
            scope.PublicationEpoch++;
            var status = ReleaseStageStatus.StagedAsSuperseded;
            if (stage.DeploymentGeneration > scope.LastSeenDeploymentGeneration)
            {
                scope.LastSeenDeploymentGeneration = stage.DeploymentGeneration;
                scope.LastSeenDeploymentReleaseId = normalized.ReleaseId;
                scope.DesiredReleaseId = normalized.ReleaseId;
                scope.DesiredIntentEpoch++;
                status = ReleaseStageStatus.StagedAsDesired;
            }

            return Task.FromResult(new ReleaseStageResult(
                status,
                normalized.ReleaseId,
                normalized.ContentHash,
                CreateVersion(normalized.SchedulerScopeKey, scope)));
        }
    }

    public Task<OwnerSnapshotPublishResult> PublishOwnerSnapshotAsync(
        JobOwnerCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeSnapshot(snapshot);

        lock (_gate)
        {
            var scope = GetScope(normalized.SchedulerScopeKey);
            var release = GetRelease(scope, normalized.ReleaseId);
            if (!release.Owners.TryGetValue(normalized.OwnerId, out var owner))
            {
                throw new JobCatalogConflictException(
                    $"Owner '{normalized.OwnerId}' is not expected by release '{normalized.ReleaseId}'.");
            }

            if (!string.Equals(owner.Manifest.WorkerRevisionId, normalized.WorkerRevisionId, StringComparison.Ordinal))
            {
                throw new JobCatalogConflictException(
                    $"Owner '{normalized.OwnerId}' published worker revision '{normalized.WorkerRevisionId}', " +
                    $"but release '{normalized.ReleaseId}' expects '{owner.Manifest.WorkerRevisionId}'.");
            }

            if (owner.Snapshot is not null)
            {
                if (!string.Equals(owner.Snapshot.ContentHash, normalized.ContentHash, StringComparison.Ordinal))
                {
                    throw new JobCatalogConflictException(
                        $"Owner '{normalized.OwnerId}' already published different declarations for release " +
                        $"'{normalized.ReleaseId}'.");
                }

                return Task.FromResult(new OwnerSnapshotPublishResult(
                    OwnerSnapshotPublishStatus.Duplicate,
                    normalized.ReleaseId,
                    normalized.OwnerId,
                    normalized.ContentHash));
            }

            var publishedKeys = release.Owners.Values
                .Where(static candidate => candidate.Snapshot is not null)
                .SelectMany(static candidate => candidate.Snapshot!.Declarations)
                .Select(static declaration => declaration.JobKey)
                .ToHashSet(StringComparer.Ordinal);
            var collision = normalized.Declarations.FirstOrDefault(declaration => publishedKeys.Contains(declaration.JobKey));
            if (collision is not null)
            {
                throw new JobCatalogConflictException(
                    $"Job key '{collision.JobKey}' is declared by more than one owner in release '{normalized.ReleaseId}'.");
            }

            owner.Snapshot = normalized;
            scope.PublicationEpoch++;
            return Task.FromResult(new OwnerSnapshotPublishResult(
                OwnerSnapshotPublishStatus.Published,
                normalized.ReleaseId,
                normalized.OwnerId,
                normalized.ContentHash));
        }
    }

    public Task<JobCatalogActivationResult> TryActivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(releaseId, nameof(releaseId));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scope = GetScope(schedulerScopeKey);
            var release = GetRelease(scope, releaseId);
            if (!string.Equals(scope.DesiredReleaseId, releaseId, StringComparison.Ordinal))
            {
                return Task.FromResult(new JobCatalogActivationResult(
                    JobCatalogActivationStatus.NotDesired,
                    CreateVersion(schedulerScopeKey, scope),
                    []));
            }

            var missingOwners = GetMissingOwners(release);
            if (missingOwners.Length != 0)
            {
                return Task.FromResult(new JobCatalogActivationResult(
                    JobCatalogActivationStatus.NotReady,
                    CreateVersion(schedulerScopeKey, scope),
                    missingOwners));
            }

            if (scope.ActiveIntentEpoch == scope.DesiredIntentEpoch)
            {
                return Task.FromResult(new JobCatalogActivationResult(
                    JobCatalogActivationStatus.AlreadyActive,
                    CreateVersion(schedulerScopeKey, scope),
                    []));
            }

            var policies = PreparePoliciesForActivation(scope, release);
            var activation = Activate(
                schedulerScopeKey,
                scope,
                release,
                policies,
                isExplicitReactivation: false);
            return Task.FromResult(new JobCatalogActivationResult(
                JobCatalogActivationStatus.Activated,
                CreateVersion(schedulerScopeKey, scope),
                [],
                CloneActivation(activation)));
        }
    }

    public Task<JobCatalogActivation> ReactivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(releaseId, nameof(releaseId));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scope = GetScope(schedulerScopeKey);
            var release = GetRelease(scope, releaseId);
            var missingOwners = GetMissingOwners(release);
            if (missingOwners.Length != 0)
            {
                throw new JobCatalogConflictException(
                    $"Release '{releaseId}' cannot be reactivated before owners [{string.Join(", ", missingOwners)}] publish.");
            }

            // Validate sticky policy against the target declarations before changing even the desired intent.
            // A rejected explicit rollback must remain a fully atomic no-op.
            var policies = PreparePoliciesForActivation(scope, release);
            scope.DesiredReleaseId = releaseId;
            scope.DesiredIntentEpoch++;
            scope.PublicationEpoch++;
            return Task.FromResult(CloneActivation(
                Activate(
                    schedulerScopeKey,
                    scope,
                    release,
                    policies,
                    isExplicitReactivation: true)));
        }
    }

    public Task<JobCatalogVersion> GetCatalogVersionAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
                ? CreateVersion(schedulerScopeKey, scope)
                : new JobCatalogVersion(schedulerScopeKey, null, null, 0, 0, 0, 0, 0, 0));
        }
    }

    public Task<JobCatalogPublicationStatus> GetCatalogPublicationStatusAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
                || scope.DesiredReleaseId is null)
            {
                return Task.FromResult(new JobCatalogPublicationStatus(
                    new JobCatalogVersion(schedulerScopeKey, null, null, 0, 0, 0, 0, 0, 0),
                    null,
                    []));
            }

            var release = GetRelease(scope, scope.DesiredReleaseId);
            var owners = release.Owners.Values
                .OrderBy(static owner => owner.Manifest.OwnerId, StringComparer.Ordinal)
                .Select(static owner => new JobCatalogOwnerPublicationStatus(
                    owner.Manifest.OwnerId,
                    owner.Manifest.WorkerRevisionId,
                    owner.Snapshot is not null,
                    owner.Snapshot?.ContentHash))
                .ToArray();
            return Task.FromResult(new JobCatalogPublicationStatus(
                CreateVersion(schedulerScopeKey, scope),
                release.Manifest.ContentHash,
                owners));
        }
    }

    public Task<JobCatalogSnapshot?> GetActiveCatalogAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_catalogScopes.TryGetValue(schedulerScopeKey, out var scope) || scope.ActiveReleaseId is null)
            {
                return Task.FromResult<JobCatalogSnapshot?>(null);
            }

            var definitions = ProjectActiveDefinitions(schedulerScopeKey, scope);
            return Task.FromResult<JobCatalogSnapshot?>(new JobCatalogSnapshot(
                CreateVersion(schedulerScopeKey, scope),
                definitions));
        }
    }

    public Task<ActiveJobDefinition?> GetActiveDefinitionAsync(
        string schedulerScopeKey,
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_catalogScopes.TryGetValue(schedulerScopeKey, out var scope) || scope.ActiveReleaseId is null)
            {
                return Task.FromResult<ActiveJobDefinition?>(null);
            }

            return Task.FromResult(ProjectActiveDefinitions(schedulerScopeKey, scope)
                .FirstOrDefault(definition => string.Equals(
                    definition.Declaration.JobKey,
                    jobKey,
                    StringComparison.Ordinal)));
        }
    }

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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IEnumerable<ActiveJobDefinition> filtered = _catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
                                                     && scope.ActiveReleaseId is not null
                ? ProjectActiveDefinitions(schedulerScopeKey, scope)
                : [];
            filtered = FilterDefinitions(filtered, query);

            var ordered = filtered.ApplyCatalogOrdering(query).ToArray();
            var page = ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult(new QueryResult<ActiveJobDefinition>(page, ordered.Length));
        }
    }

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
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scope = GetScope(schedulerScopeKey);
            if (scope.ActiveReleaseId is null
                || !ReleaseContainsJob(GetRelease(scope, scope.ActiveReleaseId), ownerId, jobKey))
            {
                throw new JobCatalogNotFoundException(
                    $"Active job '{ownerId}/{jobKey}' was not found in scope '{schedulerScopeKey}'.");
            }

            var activeDefinition = ProjectActiveDefinitions(schedulerScopeKey, scope)
                .Single(definition => string.Equals(definition.Declaration.JobKey, jobKey, StringComparison.Ordinal));
            var normalizedOverrides = change.Overrides.Normalize();
            var current = scope.Policies[jobKey];
            if (!string.Equals(current.ConcurrencyStamp, change.ExpectedConcurrencyStamp, StringComparison.Ordinal))
            {
                throw new JobPolicyConcurrencyException(jobKey);
            }
            normalizedOverrides.Validate(activeDefinition.Declaration, current.Overrides);

            var updatedAtUtc = UtcNow;
            var replacement = new JobPolicy
            {
                Overrides = normalizedOverrides,
                ConcurrencyStamp = CreateConcurrencyStamp(),
                ReviewedAgainstJobRevisionId = activeDefinition.JobRevisionId,
                RecurringScheduleEffectiveFromUtc = current.ResolveRecurringScheduleEffectiveFromUtc(
                    activeDefinition.Declaration,
                    normalizedOverrides,
                    updatedAtUtc),
                UpdatedAtUtc = updatedAtUtc
            };
            scope.Policies[jobKey] = replacement;
            scope.ChangeEpoch++;
            var updatedDefinition = activeDefinition with { Policy = replacement };
            ApplyPolicyToExecutionGateUnsafe(updatedDefinition);
            ApplyPolicyToRecurringCursorUnsafe(
                updatedDefinition,
                scope.ChangeEpoch,
                updatedAtUtc);
            return Task.FromResult(ClonePolicy(replacement));
        }
    }

    private JobCatalogActivation Activate(
        string schedulerScopeKey,
        CatalogScopeState scope,
        CatalogReleaseState release,
        IReadOnlyDictionary<string, JobPolicy> policies,
        bool isExplicitReactivation)
    {
        foreach (var (jobKey, policy) in policies)
        {
            scope.Policies[jobKey] = policy;
        }
        SynchronizeExecutionGatesForActivationUnsafe(schedulerScopeKey, scope, release);

        var nextActivationEpoch = scope.ActivationEpoch + 1;
        var activatedAtUtc = UtcNow;
        var retirement = RetireSupersededExecutionsUnsafe(
            schedulerScopeKey,
            nextActivationEpoch,
            activatedAtUtc);
        PruneSupersededRecurringCursorsUnsafe(schedulerScopeKey, nextActivationEpoch);
        scope.ActiveReleaseId = release.Manifest.ReleaseId;
        scope.ActiveIntentEpoch = scope.DesiredIntentEpoch;
        scope.ActivationEpoch = nextActivationEpoch;
        scope.ChangeEpoch++;
        var activation = new JobCatalogActivation(
            schedulerScopeKey,
            release.Manifest.ReleaseId,
            scope.ActivationEpoch,
            scope.DesiredIntentEpoch,
            activatedAtUtc,
            isExplicitReactivation,
            retirement.QueuedCount,
            retirement.RunningCount);
        scope.Activations.Add(activation);
        return activation;
    }

    private Dictionary<string, JobPolicy> PreparePoliciesForActivation(
        CatalogScopeState scope,
        CatalogReleaseState release)
    {
        var previousJobRevisions = GetActiveJobRevisionMap(scope);
        var policies = new Dictionary<string, JobPolicy>(StringComparer.Ordinal);
        foreach (var owner in release.Owners.Values)
        {
            foreach (var declaration in owner.Snapshot!.Declarations)
            {
                var incomingJobRevision = JobCatalogHash.ComputeJobRevision(
                    owner.Manifest.OwnerId,
                    owner.Manifest.WorkerRevisionId,
                    declaration);
                var hasStickyPolicy = scope.Policies.TryGetValue(declaration.JobKey, out var policy);
                policy ??= CreateDefaultPolicy(incomingJobRevision);
                try
                {
                    // Passing the persisted override set as the previous value preserves a dormant recurring schedule
                    // while the logical job is temporarily triggered, yet still validates it when recurring returns.
                    policy.Overrides.Validate(declaration, policy.Overrides);
                }
                catch (ArgumentException exception)
                {
                    throw new JobCatalogConflictException(
                        $"Sticky policy for job '{declaration.JobKey}' is incompatible with release "
                        + $"'{release.Manifest.ReleaseId}': {exception.Message}");
                }

                // A policy editor opened against the previous active code revision must not be able to overwrite
                // operator changes after cutover. Rotate only the concurrency fence; the sticky policy's review and
                // scheduling metadata continue to describe the last explicit operator save.
                if (hasStickyPolicy
                    && (!previousJobRevisions.TryGetValue(declaration.JobKey, out var previousJobRevision)
                        || !string.Equals(previousJobRevision, incomingJobRevision, StringComparison.Ordinal)))
                {
                    policy = policy with { ConcurrencyStamp = CreateConcurrencyStamp() };
                }

                policies.Add(declaration.JobKey, policy);
            }
        }

        return policies;
    }

    private static Dictionary<string, string> GetActiveJobRevisionMap(CatalogScopeState scope)
    {
        if (scope.ActiveReleaseId is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var release = GetRelease(scope, scope.ActiveReleaseId);
        return release.Owners.Values
            .SelectMany(owner => owner.Snapshot!.Declarations.Select(declaration => new
            {
                declaration.JobKey,
                JobRevisionId = JobCatalogHash.ComputeJobRevision(
                    owner.Manifest.OwnerId,
                    owner.Manifest.WorkerRevisionId,
                    declaration)
            }))
            .ToDictionary(static item => item.JobKey, static item => item.JobRevisionId, StringComparer.Ordinal);
    }

    private JobPolicy CreateDefaultPolicy(string reviewedAgainstJobRevisionId)
    {
        return new JobPolicy
        {
            Overrides = new JobPolicyOverrides(),
            ConcurrencyStamp = CreateConcurrencyStamp(),
            ReviewedAgainstJobRevisionId = reviewedAgainstJobRevisionId,
            RecurringScheduleEffectiveFromUtc = null,
            UpdatedAtUtc = UtcNow
        };
    }

    private static string CreateConcurrencyStamp() => Guid.NewGuid().ToString("N");

    private static ActiveJobDefinition[] ProjectActiveDefinitions(
        string schedulerScopeKey,
        CatalogScopeState scope)
    {
        var release = GetRelease(scope, scope.ActiveReleaseId!);
        return release.Owners.Values
            .SelectMany(owner => owner.Snapshot!.Declarations.Select(declaration => new ActiveJobDefinition
            {
                SchedulerScopeKey = schedulerScopeKey,
                ReleaseId = release.Manifest.ReleaseId,
                ActivationEpoch = scope.ActivationEpoch,
                OwnerId = owner.Manifest.OwnerId,
                WorkerRevisionId = owner.Manifest.WorkerRevisionId,
                Declaration = CloneDeclaration(declaration),
                Policy = ClonePolicy(scope.Policies[declaration.JobKey])
            }))
            .OrderBy(static definition => definition.Declaration.JobKey, StringComparer.Ordinal)
            .ToArray();
    }

    private CatalogScopeState GetOrCreateScope(string schedulerScopeKey)
    {
        if (!_catalogScopes.TryGetValue(schedulerScopeKey, out var scope))
        {
            scope = new CatalogScopeState();
            _catalogScopes.Add(schedulerScopeKey, scope);
        }
        return scope;
    }

    private CatalogScopeState GetScope(string schedulerScopeKey)
    {
        return _catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
            ? scope
            : throw new JobCatalogNotFoundException($"Scheduler scope '{schedulerScopeKey}' has no staged releases.");
    }

    private static CatalogReleaseState GetRelease(CatalogScopeState scope, string releaseId)
    {
        return scope.Releases.TryGetValue(releaseId, out var release)
            ? release
            : throw new JobCatalogNotFoundException($"Catalog release '{releaseId}' was not found.");
    }

    private static string[] GetMissingOwners(CatalogReleaseState release) => release.Owners.Values
        .Where(static owner => owner.Snapshot is null)
        .Select(static owner => owner.Manifest.OwnerId)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static bool ReleaseContainsJob(CatalogReleaseState release, string ownerId, string jobKey) =>
        release.Owners.TryGetValue(ownerId, out var owner)
        && owner.Snapshot!.Declarations.Any(declaration =>
            string.Equals(declaration.JobKey, jobKey, StringComparison.Ordinal));

    private static JobCatalogReleaseManifest NormalizeManifest(JobCatalogReleaseManifest manifest)
    {
        ValidateIdentity(manifest.SchedulerScopeKey, nameof(manifest.SchedulerScopeKey));
        ValidateIdentity(manifest.ReleaseId, nameof(manifest.ReleaseId));
        ArgumentNullException.ThrowIfNull(manifest.Owners);
        var owners = manifest.Owners
            .Select(owner =>
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
        var declarations = snapshot.Declarations.Select(static declaration => declaration.NormalizeAndValidate())
            .OrderBy(static declaration => declaration.JobKey, StringComparer.Ordinal)
            .ToArray();
        var duplicate = declarations.GroupBy(static declaration => declaration.JobKey, StringComparer.Ordinal)
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

    private static void ValidateIdentity(string value, string parameterName)
    {
        JobSchedulerIdentity.ValidateStandard(value, parameterName);
    }

    private static JobDeclaration CloneDeclaration(JobDeclaration source) => source with { };
    private static JobPolicy ClonePolicy(JobPolicy source) => source with { };
    private static JobCatalogActivation CloneActivation(JobCatalogActivation source) => source with { };
    private static JobCatalogVersion CreateVersion(string schedulerScopeKey, CatalogScopeState scope) =>
        new(
            schedulerScopeKey,
            scope.ActiveReleaseId,
            scope.DesiredReleaseId,
            scope.LastSeenDeploymentGeneration,
            scope.DesiredIntentEpoch,
            scope.ActiveIntentEpoch,
            scope.ActivationEpoch,
            scope.ChangeEpoch,
            scope.PublicationEpoch);

    private sealed class CatalogScopeState
    {
        internal Dictionary<string, CatalogReleaseState> Releases { get; } = new(StringComparer.Ordinal);
        // Policies follow the scope-wide logical job identity across owner transfers and release reactivation.
        internal Dictionary<string, JobPolicy> Policies { get; } = new(StringComparer.Ordinal);
        internal List<JobCatalogActivation> Activations { get; } = [];
        internal string? ActiveReleaseId { get; set; }
        internal string? DesiredReleaseId { get; set; }
        internal string? LastSeenDeploymentReleaseId { get; set; }
        internal long LastSeenDeploymentGeneration { get; set; }
        internal long DesiredIntentEpoch { get; set; }
        internal long ActiveIntentEpoch { get; set; }
        internal long ActivationEpoch { get; set; }
        internal long ChangeEpoch { get; set; }
        internal long PublicationEpoch { get; set; }
    }

    private sealed class CatalogReleaseState(JobCatalogReleaseManifest manifest)
    {
        internal JobCatalogReleaseManifest Manifest { get; } = manifest;
        internal Dictionary<string, CatalogOwnerState> Owners { get; } = manifest.Owners.ToDictionary(
            static owner => owner.OwnerId,
            static owner => new CatalogOwnerState(owner),
            StringComparer.Ordinal);
    }

    private sealed class CatalogOwnerState(JobCatalogOwnerManifest manifest)
    {
        internal JobCatalogOwnerManifest Manifest { get; } = manifest;
        internal JobOwnerCatalogSnapshot? Snapshot { get; set; }
    }
}
