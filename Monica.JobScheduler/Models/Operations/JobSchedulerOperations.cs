using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Models.Operations;

/// <summary>
/// Provides one operational snapshot of catalog convergence, worker capability, and durable queue state.
/// </summary>
public sealed record JobSchedulerOverview
{
    /// <summary>
    /// Gets desired-release publication and transition status.
    /// </summary>
    public required JobCatalogPublicationStatus CatalogPublication { get; init; }

    /// <summary>
    /// Gets the currently active immutable catalog, or <see langword="null"/> before first activation.
    /// </summary>
    public JobCatalogSnapshot? ActiveCatalog { get; init; }

    /// <summary>
    /// Gets queue counts grouped by durable execution state.
    /// </summary>
    public required IReadOnlyDictionary<JobExecutionState, int> ExecutionStateCounts { get; init; }

    /// <summary>
    /// Gets desired owner publication and live worker convergence.
    /// </summary>
    public required IReadOnlyList<JobOwnerConvergence> Owners { get; init; }

    /// <summary>
    /// Gets a bounded newest-first execution activity sample.
    /// </summary>
    public required IReadOnlyList<JobExecutionInstance> RecentExecutions { get; init; }

    /// <summary>
    /// Gets when the operational snapshot finished loading.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>
    /// Gets whether the desired catalog is active and every desired owner has both published and registered a worker.
    /// </summary>
    public bool IsConverged =>
        CatalogPublication.Version.ActiveReleaseId is not null
        && CatalogPublication.Version.DesiredReleaseId is not null
        && string.Equals(
            CatalogPublication.Version.ActiveReleaseId,
            CatalogPublication.Version.DesiredReleaseId,
            StringComparison.Ordinal)
        && !CatalogPublication.Version.IsTransitionInProgress
        && CatalogPublication.MissingOwnerIds.Count == 0
        && Owners.All(static owner => owner.IsConverged);
}

/// <summary>
/// Projects the independent publication and runtime-capability signals for one desired catalog owner.
/// </summary>
public sealed record JobOwnerConvergence
{
    /// <summary>
    /// Gets the stable deployment-unit identity.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the exact executable revision expected by the desired release.
    /// </summary>
    public required string WorkerRevisionId { get; init; }

    /// <summary>
    /// Gets whether the immutable declaration snapshot has been published.
    /// </summary>
    public bool HasPublishedSnapshot { get; init; }

    /// <summary>
    /// Gets the number of active worker capability leases matching the desired revision.
    /// </summary>
    public int ActiveWorkerCount { get; init; }

    /// <summary>
    /// Gets the number of definitions from this exact owner revision in the active catalog.
    /// </summary>
    public int ActiveDefinitionCount { get; init; }

    /// <summary>
    /// Gets whether both the declaration and executable-capability signals are present.
    /// </summary>
    public bool IsConverged => HasPublishedSnapshot && ActiveWorkerCount > 0;
}

/// <summary>
/// Describes one operator-initiated, catalog-resolved execution admission request.
/// </summary>
public sealed record JobTriggerRequest
{
    /// <summary>
    /// Gets the active logical job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets serialized triggered-job arguments. The payload must match the declaration's argument contract.
    /// </summary>
    public string? JobArgs { get; init; }

    /// <summary>
    /// Gets the earliest claim time. When omitted, the store atomically captures its authoritative current time while
    /// creating the execution; idempotent retries do not assert a newly computed timestamp.
    /// </summary>
    public DateTimeOffset? AvailableAtUtc { get; init; }

    /// <summary>
    /// Gets an optional idempotency identifier. A generated identifier is used when omitted.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Gets an optional expected owner used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedOwnerId { get; init; }

    /// <summary>
    /// Gets an optional expected job revision used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedJobRevisionId { get; init; }
}
