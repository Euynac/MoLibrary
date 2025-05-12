using Dapr.Client;
using Microsoft.Extensions.Options;
using MoLibrary.Dapr.Modules;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Dapr.Locker;

public class DaprMoDistributedLock(
    DaprClient client,
    IOptions<ModuleDaprLockerOption> distributedLockDaprOptions,
    IDistributedLockKeyNormalizer distributedLockKeyNormalizer)
    : IMoDistributedLock
{
    protected DaprClient Client { get; } = client;
    protected ModuleDaprLockerOption DistributedLockDaprOptions { get; } = distributedLockDaprOptions.Value;
    protected IDistributedLockKeyNormalizer DistributedLockKeyNormalizer { get; } = distributedLockKeyNormalizer;

    public async Task<IMoDistributedLockHandle?> TryAcquireAsync(
        string name,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        name = DistributedLockKeyNormalizer.NormalizeKey(name);
        var lockResponse = await client.Lock(
            DistributedLockDaprOptions.StoreName,
            name,
            DistributedLockDaprOptions.Owner ?? Guid.NewGuid().ToString(),
            (int)DistributedLockDaprOptions.DefaultExpirationTimeout.TotalSeconds,
            cancellationToken);

        if (lockResponse is not { Success: true })
        {
            return null;
        }

        return new DaprMoDistributedLockHandle(lockResponse);
    }
}
