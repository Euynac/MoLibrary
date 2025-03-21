using Medallion.Threading;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Medallion;


public class MedallionMoDistributedLock(
    IDistributedLockProvider distributedLockProvider,
    IDistributedLockKeyNormalizer distributedLockKeyNormalizer)
    : IMoDistributedLock
{
    protected IDistributedLockProvider DistributedLockProvider { get; } = distributedLockProvider;

    protected IDistributedLockKeyNormalizer DistributedLockKeyNormalizer { get; } = distributedLockKeyNormalizer;

    public async Task<IMoDistributedLockHandle?> TryAcquireAsync(
        string name,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        //Check.NotNullOrWhiteSpace(name, nameof(name));
        var key = DistributedLockKeyNormalizer.NormalizeKey(name);
        var handle = await DistributedLockProvider.TryAcquireLockAsync(
            key,
            timeout,
            cancellationToken
        );

        if (handle == null)
        {
            return null;
        }

        return new MedallionMoDistributedLockHandle(handle);
    }
}
