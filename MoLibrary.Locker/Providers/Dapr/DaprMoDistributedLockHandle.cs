using Dapr.Client;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Locker.Providers.Dapr;

public class DaprMoDistributedLockHandle(TryLockResponse lockResponse) : IMoDistributedLockHandle
{
    protected TryLockResponse LockResponse { get; } = lockResponse;

    public async ValueTask DisposeAsync()
    {
        await LockResponse.DisposeAsync();
    }
}
