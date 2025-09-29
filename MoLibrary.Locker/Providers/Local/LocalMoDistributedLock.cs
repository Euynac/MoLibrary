using System.Runtime.CompilerServices;
using AsyncKeyedLock;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Local;

public class LocalMoDistributedLock(IDistributedLockKeyNormalizer distributedLockKeyNormalizer)
    : IMoDistributedLock
{
    private readonly AsyncKeyedLocker<string> _localSyncObjects = new(o =>
    {
        o.PoolSize = 20;
        o.PoolInitialFill = 1;
    });
    protected IDistributedLockKeyNormalizer DistributedLockKeyNormalizer { get; } = distributedLockKeyNormalizer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IMoDistributedLockHandle?> TryAcquireAsync(string name,
        string? owner = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        //Check.NotNullOrWhiteSpace(name, nameof(name));
        var key = DistributedLockKeyNormalizer.NormalizeKey(name);

        var timeoutReleaser = await _localSyncObjects.LockOrNullAsync(key, timeout?? TimeSpan.FromMinutes(2), cancellationToken);
        if (timeoutReleaser is not null)
        {
            return new LocalMoDistributedLockHandle(timeoutReleaser);
        }
        return null;
    }
}
