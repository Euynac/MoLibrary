using Monica.JobScheduler.Models.Catalog;
using Monica.Modules;

namespace Monica.JobScheduler.Models;

/// <summary>
/// Captures the immutable deployment identity consumed by scheduler hosted services.
/// </summary>
internal sealed record JobSchedulerHostIdentity
{
    internal required JobCatalogReleaseStage ReleaseStage { get; init; }
    internal string? LocalOwnerId { get; init; }
    internal string? LocalWorkerRevisionId { get; init; }
    internal required string WorkerInstanceId { get; init; }

    internal static JobSchedulerHostIdentity Create(ModuleJobSchedulerOption options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var localOwnerId = options.Role is JobSchedulerRole.Worker or JobSchedulerRole.Standalone
            ? options.LocalOwnerId ?? options.GetProjectName()
            : null;
        var localRevision = localOwnerId is null
            ? null
            : options.LocalWorkerRevisionId
              ?? options.CatalogOwners.SingleOrDefault(owner => string.Equals(
                  owner.OwnerId,
                  localOwnerId,
                  StringComparison.Ordinal))?.WorkerRevisionId
              ?? throw new InvalidOperationException(
                  $"Catalog owner manifest does not contain local worker owner '{localOwnerId}'.");

        return new JobSchedulerHostIdentity
        {
            ReleaseStage = new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(
                    options.SchedulerScopeKey,
                    options.CatalogReleaseId,
                    options.CatalogOwners.ToArray()),
                options.DeploymentGeneration),
            LocalOwnerId = localOwnerId,
            LocalWorkerRevisionId = localRevision,
            WorkerInstanceId = options.WorkerInstanceId
        };
    }
}
