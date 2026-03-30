using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Locker.Services;

internal sealed class LockKeyNormalizer(IOptions<ModuleLockerOption> moduleOptions)
{
    private ModuleLockerOption ModuleOptions { get; } = moduleOptions.Value;

    public string Normalize(string resourceName)
    {
        return string.Concat(ModuleOptions.LockKeyPrefix, resourceName);
    }
}
