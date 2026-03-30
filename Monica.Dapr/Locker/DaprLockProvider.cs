using Dapr.DistributedLock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Locker.Abstractions;
using Monica.Locker.Models;
using Monica.Modules;

namespace Monica.Dapr.Locker;

#pragma warning disable DAPR_DISTRIBUTEDLOCK // DaprDistributedLockClient is evaluation API
public sealed class DaprLockProvider(
    DaprDistributedLockClient client,
    IOptions<ModuleDaprLockerOption> distributedLockDaprOptions,
    ILogger<DaprLockProvider> logger)
    : ILockProvider
#pragma warning restore DAPR_DISTRIBUTEDLOCK
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private ModuleDaprLockerOption DistributedLockDaprOptions { get; } = distributedLockDaprOptions.Value;

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string normalizedKey,
        LockAcquisitionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var waitTimeout = options.WaitTimeout ?? DistributedLockDaprOptions.DefaultLeaseDuration;
        var leaseDuration = options.LeaseDuration ?? DistributedLockDaprOptions.DefaultLeaseDuration;
        var owner = !string.IsNullOrWhiteSpace(options.Owner)
            ? options.Owner
            : $"{DistributedLockDaprOptions.LockOwnerPrefix}{Guid.NewGuid():N}";

        using var waitTimeoutSource = new CancellationTokenSource(waitTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            waitTimeoutSource.Token);
        var token = linkedSource.Token;

        try
        {
            while (true)
            {
                var lockResponse = await client.TryLockAsync(
                    DistributedLockDaprOptions.StoreName,
                    normalizedKey,
                    owner,
                    Math.Max(1, (int)Math.Ceiling(leaseDuration.TotalSeconds)),
                    token);

                if (lockResponse != null)
                {
                    return new DaprLockHandle(lockResponse);
                }

                await Task.Delay(RetryDelay, token);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Timed out acquiring Dapr distributed lock {LockKey}.", normalizedKey);
            return null;
        }
    }
}
