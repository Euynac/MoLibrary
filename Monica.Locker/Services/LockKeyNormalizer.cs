using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Locker.Services;

/// <summary>
/// Converts caller-facing resource names into provider-ready keys.
/// </summary>
internal sealed class LockKeyNormalizer(IOptions<ModuleLockerOption> moduleOptions)
{
    private ModuleLockerOption ModuleOptions { get; } = moduleOptions.Value;

    public string Normalize(string resourceName)
    {
        // Keep normalization intentionally simple so every provider observes the same stable key format.
        return string.Concat(ModuleOptions.LockKeyPrefix, resourceName);
    }
}
