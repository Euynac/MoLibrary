using Monica.Locker.Models;

namespace Monica.Locker.Abstractions;

public interface IDistributedLock
{
    /// <summary>
    /// Tries to acquire a named lock and returns a handle when successful.
    /// </summary>
    /// <param name="resourceName">The logical lock resource name.</param>
    /// <param name="options">Lock acquisition options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string resourceName,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default);
}
