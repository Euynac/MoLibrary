using Dapr.DistributedLock.Models;
using Monica.Locker.Abstractions;

namespace Monica.Dapr.Locker;

#pragma warning disable DAPR_DISTRIBUTEDLOCK // DaprDistributedLockClient is evaluation API
/// <summary>
/// Wraps Dapr's lock response so Monica code can release it through <see cref="IDistributedLockHandle"/>.
/// </summary>
internal sealed class DaprLockHandle(LockResponse lockResponse) : IDistributedLockHandle
{
    private LockResponse LockResponse { get; } = lockResponse;

    public async ValueTask DisposeAsync()
    {
        await LockResponse.DisposeAsync();
    }
}
#pragma warning restore DAPR_DISTRIBUTEDLOCK
