using Medallion.Threading;
using Monica.Locker.Abstractions;
using Monica.Locker.Models;

namespace Monica.Locker.Providers.Medallion;

public sealed class MedallionLockProvider(
    IDistributedLockProvider distributedLockProvider)
    : ILockProvider
{
    private IDistributedLockProvider DistributedLockProvider { get; } = distributedLockProvider;

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string normalizedKey,
        LockAcquisitionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var waitTimeout = options.WaitTimeout ?? TimeSpan.FromMinutes(2);
        var handle = await DistributedLockProvider.TryAcquireLockAsync(
            normalizedKey,
            waitTimeout,
            cancellationToken
        );

        return handle is null ? null : new MedallionLockHandle(handle);
    }
}
