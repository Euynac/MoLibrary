namespace MoLibrary.Locker.DistributedLocking;

public interface IDistributedLockKeyNormalizer
{
    string NormalizeKey(string name);

}