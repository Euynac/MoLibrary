﻿using Dapr.Client;
using Microsoft.Extensions.Options;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Dapr;

public class DaprMoDistributedLock(
    DaprClient client,
    IOptions<MoDistributedLockDaprOptions> distributedLockDaprOptions,
    IDistributedLockKeyNormalizer distributedLockKeyNormalizer)
    : IMoDistributedLock
{
    protected DaprClient Client { get; } = client;
    protected MoDistributedLockDaprOptions DistributedLockDaprOptions { get; } = distributedLockDaprOptions.Value;
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
