using Microsoft.Extensions.Options;

namespace MoLibrary.Locker.DistributedLocking;

public class DistributedLockKeyNormalizer(IOptions<MoDistributedLockOptions> options) : IDistributedLockKeyNormalizer
{
    protected MoDistributedLockOptions Options { get; } = options.Value;

    public virtual string NormalizeKey(string name)
    {
        return $"{Options.KeyPrefix}{name}";
    }
}