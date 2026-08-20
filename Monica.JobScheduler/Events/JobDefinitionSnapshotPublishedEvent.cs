using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Events;

/// <summary>
/// Publishes the complete set of jobs owned by one worker project to the scheduler control plane.
/// </summary>
/// <remarks>
/// The event is a replace-style snapshot rather than a collection of deltas. Repeated delivery is safe, an empty
/// snapshot removes definitions retired by that project, and the source build timestamp prevents an older deployment
/// replica from overwriting a newer declaration set during a rolling upgrade.
/// </remarks>
public sealed class JobDefinitionSnapshotPublishedEvent
{
    /// <summary>
    /// Gets the scheduler scope to which the snapshot belongs.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the project that owns every definition in the snapshot.
    /// </summary>
    public required string OwnerProject { get; init; }

    /// <summary>
    /// Gets the service-discovery application identifier of the publisher.
    /// </summary>
    public required string SourceAppId { get; init; }

    /// <summary>
    /// Gets the concrete service instance that published this copy of the snapshot.
    /// </summary>
    public required string SourceInstanceId { get; init; }

    /// <summary>
    /// Gets the UTC build timestamp used to order publications from overlapping deployments.
    /// </summary>
    public DateTimeOffset SourceBuildTimeUtc { get; init; }

    /// <summary>
    /// Gets the optional application release identifier for diagnostics.
    /// </summary>
    public string? SourceReleaseVersion { get; init; }

    /// <summary>
    /// Gets the deterministic SHA-256 fingerprint of the scope, owner, and ordered declarations.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Gets when this publication attempt was created in UTC.
    /// </summary>
    public DateTimeOffset PublishedAtUtc { get; init; }

    /// <summary>
    /// Gets the complete declaration set owned by <see cref="OwnerProject"/>. An empty collection is meaningful.
    /// </summary>
    public IReadOnlyList<JobDefinitionDeclaration> Definitions { get; init; } = [];

    internal static JobDefinitionSnapshotPublishedEvent Create(
        string schedulerScopeKey,
        string ownerProject,
        string sourceAppId,
        string sourceInstanceId,
        DateTime sourceBuildTimeUtc,
        string? sourceReleaseVersion,
        IEnumerable<JobDefinition> definitions,
        DateTime? publishedAtUtc = null)
    {
        return Create(
            schedulerScopeKey,
            ownerProject,
            sourceAppId,
            sourceInstanceId,
            new DateTimeOffset(NormalizeUtc(sourceBuildTimeUtc)),
            sourceReleaseVersion,
            definitions,
            publishedAtUtc is { } value
                ? new DateTimeOffset(NormalizeUtc(value))
                : null);
    }

    internal static JobDefinitionSnapshotPublishedEvent Create(
        string schedulerScopeKey,
        string ownerProject,
        string sourceAppId,
        string sourceInstanceId,
        DateTimeOffset sourceBuildTimeUtc,
        string? sourceReleaseVersion,
        IEnumerable<JobDefinition> definitions,
        DateTimeOffset? publishedAtUtc = null)
    {
        var declarations = definitions
            .Select(JobDefinitionDeclaration.FromDefinition)
            .OrderBy(static definition => definition.JobKey, StringComparer.Ordinal)
            .ToArray();

        return new JobDefinitionSnapshotPublishedEvent
        {
            SchedulerScopeKey = schedulerScopeKey,
            OwnerProject = ownerProject,
            SourceAppId = sourceAppId,
            SourceInstanceId = sourceInstanceId,
            SourceBuildTimeUtc = sourceBuildTimeUtc.ToUniversalTime(),
            SourceReleaseVersion = sourceReleaseVersion,
            ContentHash = JobDefinitionSnapshotFingerprint.Compute(schedulerScopeKey, ownerProject, declarations),
            PublishedAtUtc = (publishedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            Definitions = declarations
        };
    }

    internal void Validate(string expectedSchedulerScopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSchedulerScopeKey);

        if (!string.Equals(SchedulerScopeKey, expectedSchedulerScopeKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Job-definition snapshot scope '{SchedulerScopeKey}' does not match '{expectedSchedulerScopeKey}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ContentHash);

        if (SourceBuildTimeUtc == default || SourceBuildTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "A job-definition snapshot must declare its source build time in UTC.");
        }

        if (Definitions is null)
        {
            throw new InvalidOperationException(
                "A job-definition snapshot must include its complete definition collection, which may be empty.");
        }

        var duplicate = Definitions
            .GroupBy(static definition => definition.JobKey, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Job-definition snapshot for '{OwnerProject}' contains duplicate key '{duplicate.Key}'.");
        }

        foreach (var definition in Definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.JobKey);
            if (!string.Equals(definition.FromProject, OwnerProject, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Job '{definition.JobKey}' is owned by '{definition.FromProject}', not snapshot owner '{OwnerProject}'.");
            }

            if (definition.MaxConcurrency < 1)
            {
                throw new InvalidOperationException(
                    $"Job '{definition.JobKey}' must allow at least one concurrent execution.");
            }

            if (definition.RetryCount < 0 || definition.MaxExecutionTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Job '{definition.JobKey}' declares an invalid retry count or execution timeout.");
            }
        }

        var expectedHash = JobDefinitionSnapshotFingerprint.Compute(
            SchedulerScopeKey,
            OwnerProject,
            Definitions);
        if (!string.Equals(ContentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Job-definition snapshot fingerprint mismatch for '{OwnerProject}'.");
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
