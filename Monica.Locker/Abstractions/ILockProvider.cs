using Monica.Locker.Models;

namespace Monica.Locker.Abstractions;

public interface ILockProvider
{
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string normalizedKey,
        LockAcquisitionOptions options,
        CancellationToken cancellationToken = default);
}
