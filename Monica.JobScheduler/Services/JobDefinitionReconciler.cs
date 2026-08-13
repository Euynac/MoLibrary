using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.Modules;
using Monica.StateStore.Abstractions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Serializes project-owned definition snapshots into the persistent scheduler catalog.
/// </summary>
/// <remarks>
/// Only the elected scheduler control plane invokes this service. A durable cursor in the service-discovery state
/// store rejects stale deployment replicas and is committed only after downstream change notification succeeds.
/// </remarks>
internal sealed class JobDefinitionReconciler(
    IJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleServiceDiscovery))] IStateStore publicationCursorStore,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobDefinitionReconciler> logger)
{
    private const string CURSOR_KEY_PREFIX = "job-scheduler:definition-publication:";
    private readonly SemaphoreSlim _reconciliationGate = new(1, 1);
    private readonly ModuleJobSchedulerOption _options = options.Value;

    internal async Task<JobDefinitionSnapshotApplyResult> ApplyAsync(
        JobDefinitionSnapshotPublishedEvent snapshot,
        Func<JobDefinitionSnapshotApplyResult, CancellationToken, Task> notifyChanges,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(notifyChanges);
        snapshot.Validate(_options.SchedulerScopeKey);

        await _reconciliationGate.WaitAsync(cancellationToken);
        try
        {
            var cursorKey = GetCursorKey(snapshot.SchedulerScopeKey, snapshot.OwnerProject);
            var (cursor, cursorETag) = await publicationCursorStore
                .GetStateAndETagAsync<JobDefinitionPublicationCursor>(cursorKey, cancellationToken);

            var decision = Decide(cursor, snapshot);
            if (decision == PublicationDecision.Stale)
            {
                logger.LogWarning(
                    "Ignored stale job-definition snapshot {ContentHash} for {OwnerProject} from build {BuildTime}; " +
                    "accepted build is {AcceptedBuildTime}",
                    snapshot.ContentHash,
                    snapshot.OwnerProject,
                    snapshot.SourceBuildTimeUtc,
                    cursor!.SourceBuildTimeUtc);
                return JobDefinitionSnapshotApplyResult.Stale(snapshot);
            }

            if (decision == PublicationDecision.Duplicate)
            {
                logger.LogDebug(
                    "Ignored duplicate job-definition snapshot {ContentHash} for {OwnerProject}",
                    snapshot.ContentHash,
                    snapshot.OwnerProject);
                return JobDefinitionSnapshotApplyResult.Duplicate(snapshot);
            }

            if (decision == PublicationDecision.ConflictingBuild)
            {
                throw new InvalidOperationException(
                    $"Project '{snapshot.OwnerProject}' published different job-definition snapshots for build " +
                    $"'{snapshot.SourceBuildTimeUtc:O}'. Accepted hash: '{cursor!.ContentHash}'; " +
                    $"received hash: '{snapshot.ContentHash}'.");
            }

            var result = decision == PublicationDecision.NewerBuildWithIdenticalContent
                ? JobDefinitionSnapshotApplyResult.Unchanged(snapshot)
                : await ReconcileDefinitionsAsync(snapshot, cursor, cancellationToken);

            if (result.RequiresNotification)
            {
                await notifyChanges(result, cancellationToken);
            }

            await SaveCursorAsync(cursorKey, cursorETag, cursor, snapshot, cancellationToken);

            logger.LogInformation(
                "Applied job-definition snapshot {ContentHash} for {OwnerProject}: {AddedCount} added, " +
                "{UpdatedCount} updated, {DeletedCount} deleted",
                snapshot.ContentHash,
                snapshot.OwnerProject,
                result.AddedDefinitions.Count,
                result.UpdatedDefinitions.Count,
                result.DeletedJobKeys.Count);
            return result;
        }
        finally
        {
            _reconciliationGate.Release();
        }
    }

    private async Task<JobDefinitionSnapshotApplyResult> ReconcileDefinitionsAsync(
        JobDefinitionSnapshotPublishedEvent snapshot,
        JobDefinitionPublicationCursor? cursor,
        CancellationToken cancellationToken)
    {
        var queryResult = await metadataRepository.QueryDefinitionsAsync(
            new JobDefinitionQuery
            {
                FromProject = snapshot.OwnerProject,
                IncludeDeleted = true,
                PageNumber = 1,
                PageSize = int.MaxValue
            },
            cancellationToken);
        var existingByKey = queryResult.Items.ToDictionary(static definition => definition.JobKey, StringComparer.Ordinal);
        var currentKeys = snapshot.Definitions
            .Select(static definition => definition.JobKey)
            .ToHashSet(StringComparer.Ordinal);
        var effectiveByKey = new Dictionary<string, JobDefinition>(StringComparer.Ordinal);
        var previouslyAcceptedKeys = cursor?.AcceptedJobKeys?.ToHashSet(StringComparer.Ordinal);

        foreach (var declaration in snapshot.Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            existingByKey.TryGetValue(declaration.JobKey, out var existing);
            if (existing is null)
            {
                var conflictingOwner = await metadataRepository.GetDefinitionAsync(
                    declaration.JobKey,
                    cancellationToken);
                if (conflictingOwner is not null)
                {
                    throw new InvalidOperationException(
                        $"Job key '{declaration.JobKey}' is already owned by project " +
                        $"'{conflictingOwner.FromProject}' and cannot be claimed by '{snapshot.OwnerProject}'.");
                }
            }

            var effective = declaration.Materialize(snapshot.SchedulerScopeKey, existing);
            effectiveByKey.Add(effective.JobKey, effective);

            if (existing is null)
            {
                await metadataRepository.SaveDefinitionAsync(effective, cancellationToken);
                continue;
            }

            if (HasSamePersistedState(existing, effective))
            {
                continue;
            }

            await metadataRepository.SaveDefinitionAsync(effective, cancellationToken);
        }

        foreach (var existing in existingByKey.Values.Where(definition => !currentKeys.Contains(definition.JobKey)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!existing.IsDeleted)
            {
                var retired = Copy(existing, isDeleted: true, deletedAt: DateTime.UtcNow);
                await metadataRepository.SaveDefinitionAsync(retired, cancellationToken);
            }
        }

        // Without a committed cursor, persistence may be the residue of a failed notification attempt. Report a
        // complete refresh rather than guessing which rows downstream consumers observed.
        JobDefinition[] added = previouslyAcceptedKeys is null
            ? []
            : effectiveByKey.Values
                .Where(definition => !previouslyAcceptedKeys.Contains(definition.JobKey))
                .ToArray();
        var refreshed = previouslyAcceptedKeys is null
            ? effectiveByKey.Values.ToArray()
            : effectiveByKey.Values
                .Where(definition => previouslyAcceptedKeys.Contains(definition.JobKey))
                .ToArray();
        var retiredKeys = existingByKey.Values
            .Where(definition => !currentKeys.Contains(definition.JobKey))
            .Where(definition => previouslyAcceptedKeys is null
                                 || !definition.IsDeleted
                                 || previouslyAcceptedKeys.Contains(definition.JobKey))
            .Select(static definition => definition.JobKey)
            .ToArray();

        // A snapshot is one authoritative unit. Publish its complete effective state before committing the cursor,
        // even when an earlier attempt already persisted part of it. This makes notification failure retry-safe.
        return JobDefinitionSnapshotApplyResult.Applied(snapshot, added, refreshed, retiredKeys);
    }

    private async Task SaveCursorAsync(
        string cursorKey,
        string cursorETag,
        JobDefinitionPublicationCursor? current,
        JobDefinitionSnapshotPublishedEvent snapshot,
        CancellationToken cancellationToken)
    {
        var replacement = new JobDefinitionPublicationCursor
        {
            OwnerProject = snapshot.OwnerProject,
            SourceBuildTimeUtc = snapshot.SourceBuildTimeUtc,
            SourceReleaseVersion = snapshot.SourceReleaseVersion,
            ContentHash = snapshot.ContentHash,
            SourceInstanceId = snapshot.SourceInstanceId,
            AcceptedJobKeys = snapshot.Definitions
                .Select(static definition => definition.JobKey)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ReconciledAtUtc = DateTimeOffset.UtcNow
        };

        var saved = current is null
            ? await publicationCursorStore.TrySaveStateIfNotExistsAsync(
                cursorKey,
                replacement,
                cancellationToken,
                TimeSpan.Zero)
            : await publicationCursorStore.TrySaveStateWithETagWithoutReadBackAsync(
                cursorKey,
                replacement,
                cursorETag,
                cancellationToken,
                TimeSpan.Zero);

        if (!saved)
        {
            throw new InvalidOperationException(
                $"Job-definition publication cursor changed concurrently for '{snapshot.OwnerProject}'.");
        }
    }

    private static PublicationDecision Decide(
        JobDefinitionPublicationCursor? cursor,
        JobDefinitionSnapshotPublishedEvent snapshot)
    {
        if (cursor is null)
        {
            return PublicationDecision.Apply;
        }

        var buildComparison = snapshot.SourceBuildTimeUtc.CompareTo(cursor.SourceBuildTimeUtc);
        if (buildComparison < 0)
        {
            return PublicationDecision.Stale;
        }

        if (buildComparison == 0)
        {
            return string.Equals(snapshot.ContentHash, cursor.ContentHash, StringComparison.OrdinalIgnoreCase)
                ? PublicationDecision.Duplicate
                : PublicationDecision.ConflictingBuild;
        }

        return string.Equals(snapshot.ContentHash, cursor.ContentHash, StringComparison.OrdinalIgnoreCase)
            ? PublicationDecision.NewerBuildWithIdenticalContent
            : PublicationDecision.Apply;
    }

    private static string GetCursorKey(string schedulerScopeKey, string ownerProject)
    {
        var identity = Encoding.UTF8.GetBytes($"{schedulerScopeKey}\0{ownerProject}");
        return CURSOR_KEY_PREFIX + Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    private static bool HasSamePersistedState(JobDefinition left, JobDefinition right)
    {
        return string.Equals(left.SchedulerScopeKey, right.SchedulerScopeKey, StringComparison.Ordinal)
               && string.Equals(left.JobKey, right.JobKey, StringComparison.Ordinal)
               && string.Equals(left.JobArgsKey, right.JobArgsKey, StringComparison.Ordinal)
               && string.Equals(left.FromProject, right.FromProject, StringComparison.Ordinal)
               && string.Equals(left.JobName, right.JobName, StringComparison.Ordinal)
               && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
               && left.JobType == right.JobType
               && left.MaxConcurrency == right.MaxConcurrency
               && left.RetryCount == right.RetryCount
               && left.MaxExecutionTimeout == right.MaxExecutionTimeout
               && left.IsDisabled == right.IsDisabled
               && left.IsDeleted == right.IsDeleted
               && left.DeletedAt == right.DeletedAt
               && left.MaxRetainedHistoryRecords == right.MaxRetainedHistoryRecords
               && left.MaxRetentionDays == right.MaxRetentionDays
               && string.Equals(left.CronExpression, right.CronExpression, StringComparison.Ordinal)
               && left.StartTime == right.StartTime
               && left.EndTime == right.EndTime;
    }

    private static JobDefinition Copy(JobDefinition source, bool isDeleted, DateTime? deletedAt)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = source.SchedulerScopeKey,
            JobKey = source.JobKey,
            JobArgsKey = source.JobArgsKey,
            FromProject = source.FromProject,
            JobName = source.JobName,
            Description = source.Description,
            JobType = source.JobType,
            MaxConcurrency = source.MaxConcurrency,
            RetryCount = source.RetryCount,
            MaxExecutionTimeout = source.MaxExecutionTimeout,
            IsDisabled = source.IsDisabled,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            MaxRetainedHistoryRecords = source.MaxRetainedHistoryRecords,
            MaxRetentionDays = source.MaxRetentionDays,
            CronExpression = source.CronExpression,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            JobClrType = source.JobClrType,
            JobArgsClrType = source.JobArgsClrType
        };
    }

    private enum PublicationDecision
    {
        Apply,
        Duplicate,
        Stale,
        ConflictingBuild,
        NewerBuildWithIdenticalContent
    }
}

internal sealed class JobDefinitionPublicationCursor
{
    public required string OwnerProject { get; init; }
    public DateTimeOffset SourceBuildTimeUtc { get; init; }
    public string? SourceReleaseVersion { get; init; }
    public required string ContentHash { get; init; }
    public required string SourceInstanceId { get; init; }
    public IReadOnlyList<string> AcceptedJobKeys { get; init; } = [];
    public DateTimeOffset ReconciledAtUtc { get; init; }
}

internal sealed record JobDefinitionSnapshotApplyResult(
    JobDefinitionSnapshotPublishedEvent Snapshot,
    IReadOnlyList<JobDefinition> AddedDefinitions,
    IReadOnlyList<JobDefinition> UpdatedDefinitions,
    IReadOnlyList<string> DeletedJobKeys,
    bool WasDuplicate,
    bool WasStale)
{
    internal bool RequiresNotification =>
        AddedDefinitions.Count != 0 || UpdatedDefinitions.Count != 0 || DeletedJobKeys.Count != 0;

    internal static JobDefinitionSnapshotApplyResult Applied(
        JobDefinitionSnapshotPublishedEvent snapshot,
        IReadOnlyList<JobDefinition> added,
        IReadOnlyList<JobDefinition> updated,
        IReadOnlyList<string> deleted) =>
        new(snapshot, added, updated, deleted, WasDuplicate: false, WasStale: false);

    internal static JobDefinitionSnapshotApplyResult Duplicate(JobDefinitionSnapshotPublishedEvent snapshot) =>
        new(snapshot, [], [], [], WasDuplicate: true, WasStale: false);

    internal static JobDefinitionSnapshotApplyResult Stale(JobDefinitionSnapshotPublishedEvent snapshot) =>
        new(snapshot, [], [], [], WasDuplicate: false, WasStale: true);

    internal static JobDefinitionSnapshotApplyResult Unchanged(JobDefinitionSnapshotPublishedEvent snapshot) =>
        new(snapshot, [], [], [], WasDuplicate: false, WasStale: false);
}
