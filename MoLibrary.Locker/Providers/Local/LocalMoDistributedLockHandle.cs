using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Local;

public class LocalMoDistributedLockHandle(IDisposable disposable) : IMoDistributedLockHandle
{
    public ValueTask DisposeAsync()
    {
        disposable.Dispose();
        return default;
    }
}
