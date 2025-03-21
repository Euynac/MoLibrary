using Medallion.Threading;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Medallion;

public class MedallionMoDistributedLockHandle(IDistributedSynchronizationHandle handle) : IMoDistributedLockHandle
{
    public IDistributedSynchronizationHandle Handle { get; } = handle;

    public ValueTask DisposeAsync()
    {
        return Handle.DisposeAsync();
    }
}
