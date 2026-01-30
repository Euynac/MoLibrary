using Monica.Locker.DistributedLocking;

namespace Monica.Locker.Providers.Local;

public class LocalMoDistributedLockHandle(IDisposable disposable) : IMoDistributedLockHandle
{
    public ValueTask DisposeAsync()
    {
        disposable.Dispose();
        return default;
    }
}
