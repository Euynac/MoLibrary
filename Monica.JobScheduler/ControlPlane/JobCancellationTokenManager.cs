using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.StateStore.CancellationManager;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Modules;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Implementation of <see cref="IJobCancellationTokenManager"/> that wraps <see cref="IMoCancellationManager"/>
/// and automatically prefixes all job instance token keys with "JobScheduler:" for namespace isolation.
/// </summary>
public class JobCancellationTokenManager(
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoCancellationManager cancellationManager,
    ILogger<JobCancellationTokenManager> logger) : IJobCancellationTokenManager
{
    private const string JobTokenPrefix = "JobScheduler:";

    /// <inheritdoc />
    public async Task<CancellationToken> GetOrCreateJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default)
    {
        var key = GetPrefixedKey(jobInstanceId);

        logger.LogDebug(
            "Getting or creating cancellation token for job instance {JobInstanceId} with key {Key}",
            jobInstanceId,
            key);

        return await cancellationManager.GetOrCreateTokenAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default)
    {
        var key = GetPrefixedKey(jobInstanceId);

        logger.LogDebug(
            "Cancelling token for job instance {JobInstanceId} with key {Key}",
            jobInstanceId,
            key);

        await cancellationManager.CancelTokenAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default)
    {
        var key = GetPrefixedKey(jobInstanceId);

        logger.LogDebug(
            "Deleting token for job instance {JobInstanceId} with key {Key}",
            jobInstanceId,
            key);

        await cancellationManager.DeleteTokenAsync(key, cancellationToken);
    }

    private static string GetPrefixedKey(string jobInstanceId) => $"{JobTokenPrefix}{jobInstanceId}";
}
