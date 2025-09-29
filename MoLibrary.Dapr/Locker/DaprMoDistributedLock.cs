using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Dapr.Modules;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Dapr.Locker;

public class DaprMoDistributedLock(
    DaprClient client,
    IOptions<ModuleDaprLockerOption> distributedLockDaprOptions,
    IDistributedLockKeyNormalizer distributedLockKeyNormalizer,
    ILogger<DaprMoDistributedLock> logger)
    : IMoDistributedLock
{
    protected ModuleDaprLockerOption DistributedLockDaprOptions { get; } = distributedLockDaprOptions.Value;
    protected IDistributedLockKeyNormalizer DistributedLockKeyNormalizer { get; } = distributedLockKeyNormalizer;

    public async Task<IMoDistributedLockHandle?> TryAcquireAsync(string name,
        string? owner = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        name = DistributedLockKeyNormalizer.NormalizeKey(name);
        try
        {
            timeout ??= DistributedLockDaprOptions.DefaultExpirationTimeout;
            using var source = new CancellationTokenSource(timeout.Value);
            var token = source.Token;
            
            while (true)
            {
                var lockResponse = await client.Lock(
                    DistributedLockDaprOptions.StoreName,
                    name,
                    owner ?? DistributedLockDaprOptions.OwnerPrefix + Guid.NewGuid().ToString(),
                    (int)timeout.Value.TotalSeconds,
                    token);
                
                if (token.IsCancellationRequested)
                {
                    return null;
                }
                
                if (lockResponse is { Success: true })
                {
                    return new DaprMoDistributedLockHandle(lockResponse);
                }
                await Task.Delay(100, token).WaitAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogError($"获取分布式锁超时：{name}");
            return null;

        }
       
    }
}
