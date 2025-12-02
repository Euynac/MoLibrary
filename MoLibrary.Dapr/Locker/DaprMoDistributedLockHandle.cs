using Dapr.DistributedLock.Models;
using MoLibrary.Locker.DistributedLocking;

namespace MoLibrary.Dapr.Locker;

#pragma warning disable DAPR_DISTRIBUTEDLOCK // DaprDistributedLockClient is evaluation API
public class DaprMoDistributedLockHandle(LockResponse lockResponse) : IMoDistributedLockHandle
{
    protected LockResponse LockResponse { get; } = lockResponse;

    public async ValueTask DisposeAsync()
    {
        await LockResponse.DisposeAsync();
    }
}
#pragma warning restore DAPR_DISTRIBUTEDLOCK
