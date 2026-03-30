using Medallion.Threading;
using Monica.Locker.Abstractions;

namespace Monica.Locker.Providers.Medallion;

internal sealed class MedallionLockHandle(IDistributedSynchronizationHandle handle) : IDistributedLockHandle
{
    public IDistributedSynchronizationHandle Handle { get; } = handle;

    public ValueTask DisposeAsync()
    {
        return Handle.DisposeAsync();
    }
}
