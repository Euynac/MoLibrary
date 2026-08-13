using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Persists immutable scheduler releases and the independently mutable operator policy projection.
/// </summary>
public interface IJobCatalogStore
{
    Task<ReleaseStageResult> StageReleaseAsync(
        JobCatalogReleaseStage stage,
        CancellationToken cancellationToken = default);

    Task<OwnerSnapshotPublishResult> PublishOwnerSnapshotAsync(
        JobOwnerCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<JobCatalogActivationResult> TryActivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default);

    Task<JobCatalogActivation> ReactivateReleaseAsync(
        string schedulerScopeKey,
        string releaseId,
        CancellationToken cancellationToken = default);

    Task<JobCatalogVersion> GetCatalogVersionAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default);

    Task<JobCatalogPublicationStatus> GetCatalogPublicationStatusAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default);

    Task<JobCatalogSnapshot?> GetActiveCatalogAsync(
        string schedulerScopeKey,
        CancellationToken cancellationToken = default);

    Task<ActiveJobDefinition?> GetActiveDefinitionAsync(
        string schedulerScopeKey,
        string jobKey,
        CancellationToken cancellationToken = default);

    Task<QueryResult<ActiveJobDefinition>> QueryActiveDefinitionsAsync(
        string schedulerScopeKey,
        JobCatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the policy owned by the scope-wide logical job after verifying its current active owner.
    /// </summary>
    /// <remarks>
    /// Policy identity is <paramref name="schedulerScopeKey"/> plus <paramref name="jobKey"/> and survives owner
    /// transfers and release reactivation. <paramref name="ownerId"/> is an optimistic active-catalog fence only.
    /// </remarks>
    Task<JobPolicy> UpdatePolicyAsync(
        string schedulerScopeKey,
        string ownerId,
        string jobKey,
        JobPolicyChange change,
        CancellationToken cancellationToken = default);
}
