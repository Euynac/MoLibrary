using Dapr.DistributedLock.Models;
using Monica.Locker.Abstractions;

namespace Monica.Dapr.Locker;

#pragma warning disable DAPR_DISTRIBUTEDLOCK // DaprDistributedLockClient is evaluation API
internal sealed class DaprLockHandle(LockResponse lockResponse) : IDistributedLockHandle
{
    private LockResponse LockResponse { get; } = lockResponse;

    public async ValueTask DisposeAsync()
    {
        await LockResponse.DisposeAsync();
    }
}
#pragma warning restore DAPR_DISTRIBUTEDLOCK
