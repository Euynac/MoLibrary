using Microsoft.Extensions.Options;
using Monica.Locker.Modules;

namespace Monica.Locker.DistributedLocking;

public class DistributedLockKeyNormalizer(IOptions<ModuleLockerOption> options) : IDistributedLockKeyNormalizer
{
    protected ModuleLockerOption Options { get; } = options.Value;

    public virtual string NormalizeKey(string name)
    {
        return $"{Options.KeyPrefix}{name}";
    }
}