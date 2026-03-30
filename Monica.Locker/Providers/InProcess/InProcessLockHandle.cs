using Monica.Locker.Abstractions;

namespace Monica.Locker.Providers.InProcess;

internal sealed class InProcessLockHandle(IDisposable disposable) : IDistributedLockHandle
{
    public ValueTask DisposeAsync()
    {
        disposable.Dispose();
        return default;
    }
}
