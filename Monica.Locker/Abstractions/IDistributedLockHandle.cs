namespace Monica.Locker.Abstractions;

/// <summary>
/// Represents an acquired lock that must be disposed to release the underlying provider resource.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable
{

}
