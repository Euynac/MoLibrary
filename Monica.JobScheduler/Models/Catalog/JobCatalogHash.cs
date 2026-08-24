using System.Security.Cryptography;
using System.Text.Json;

namespace Monica.JobScheduler.Models.Catalog;

internal static class JobCatalogHash
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web);

    internal static string ComputeManifest(JobCatalogReleaseManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var canonical = new
        {
            manifest.SchedulerScopeKey,
            manifest.ReleaseId,
            Owners = manifest.Owners
                .OrderBy(static owner => owner.OwnerId, StringComparer.Ordinal)
                .Select(static owner => new { owner.OwnerId, owner.WorkerRevisionId })
                .ToArray()
        };
        return Hash(canonical);
    }

    internal static string ComputeSnapshot(JobOwnerCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var canonical = new
        {
            snapshot.SchedulerScopeKey,
            snapshot.ReleaseId,
            snapshot.OwnerId,
            snapshot.WorkerRevisionId,
            Declarations = snapshot.Declarations
                .OrderBy(static declaration => declaration.JobKey, StringComparer.Ordinal)
                .Select(static declaration => new
                {
                    declaration.JobKey,
                    declaration.JobArgsKey,
                    declaration.JobName,
                    declaration.Description,
                    JobType = (int)declaration.JobType,
                    declaration.MaxConcurrency,
                    declaration.RetryCount,
                    MaxExecutionTimeoutTicks = declaration.MaxExecutionTimeout.Ticks,
                    declaration.IsDisabledByDefault,
                    declaration.CronExpression,
                    declaration.TimeZoneId,
                    StartTimeUtcTicks = declaration.StartTimeUtc?.UtcTicks,
                    EndTimeUtcTicks = declaration.EndTimeUtc?.UtcTicks
                })
                .ToArray()
        };
        return Hash(canonical);
    }

    internal static string ComputeJobRevision(
        string ownerId,
        string workerRevisionId,
        JobDeclaration declaration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRevisionId);
        ArgumentNullException.ThrowIfNull(declaration);
        return Hash(new
        {
            OwnerId = ownerId,
            WorkerRevisionId = workerRevisionId,
            declaration.JobKey,
            declaration.JobArgsKey,
            declaration.JobName,
            declaration.Description,
            JobType = (int)declaration.JobType,
            declaration.MaxConcurrency,
            declaration.RetryCount,
            MaxExecutionTimeoutTicks = declaration.MaxExecutionTimeout.Ticks,
            declaration.IsDisabledByDefault,
            declaration.CronExpression,
            declaration.TimeZoneId,
            StartTimeUtcTicks = declaration.StartTimeUtc?.UtcTicks,
            EndTimeUtcTicks = declaration.EndTimeUtc?.UtcTicks
        });
    }

    private static string Hash<T>(T value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JSON_OPTIONS)));
    }
}
