namespace MoLibrary.Locker.DistributedLocking;

public class MoDistributedLockOptions
{
    /// <summary>
    /// DistributedLock key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "";
}