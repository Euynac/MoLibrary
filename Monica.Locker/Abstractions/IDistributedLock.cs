using Monica.Locker.Models;

namespace Monica.Locker.Abstractions;

/// <summary>
/// Exposes the public lock acquisition entry point for Monica locker consumers.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Tries to acquire a named lock and returns a handle when successful.
    /// </summary>
    /// <param name="resourceName">The logical resource name before provider-specific key normalization is applied.</param>
    /// <param name="options">Optional acquisition settings such as owner, wait timeout, and lease duration.</param>
    /// <param name="cancellationToken">Cancels the acquisition attempt.</param>
    /// <returns>
    /// A disposable lock handle when the lock is acquired; otherwise <see langword="null"/> when acquisition times out
    /// or the operation is canceled before a lock is obtained.
    /// </returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string resourceName,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default);
}
