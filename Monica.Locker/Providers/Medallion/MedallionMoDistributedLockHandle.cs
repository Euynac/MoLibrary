using Medallion.Threading;
using Monica.Locker.DistributedLocking;

namespace Monica.Locker.Providers.Medallion;

public class MedallionMoDistributedLockHandle(IDistributedSynchronizationHandle handle) : IMoDistributedLockHandle
{
    public IDistributedSynchronizationHandle Handle { get; } = handle;

    public ValueTask DisposeAsync()
    {
        return Handle.DisposeAsync();
    }
}
