namespace Monica.Locker.DistributedLocking;

public interface IDistributedLockKeyNormalizer
{
    string NormalizeKey(string name);

}