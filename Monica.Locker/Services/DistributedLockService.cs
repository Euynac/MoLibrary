using Microsoft.Extensions.Options;
using Monica.Locker.Abstractions;
using Monica.Locker.Models;
using Monica.Modules;

namespace Monica.Locker.Services;

internal sealed class DistributedLockService(
    ILockProvider lockProvider,
    LockKeyNormalizer keyNormalizer,
    IOptions<ModuleLockerOption> moduleOptions)
    : IDistributedLock
{
    private ModuleLockerOption ModuleOptions { get; } = moduleOptions.Value;

    public Task<IDistributedLockHandle?> TryAcquireAsync(
        string resourceName,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var normalizedKey = keyNormalizer.Normalize(resourceName);
        var effectiveOptions = CreateEffectiveOptions(options);
        return lockProvider.TryAcquireAsync(normalizedKey, effectiveOptions, cancellationToken);
    }

    private LockAcquisitionOptions CreateEffectiveOptions(LockAcquisitionOptions? options)
    {
        return new LockAcquisitionOptions
        {
            Owner = options?.Owner,
            WaitTimeout = options?.WaitTimeout ?? ModuleOptions.DefaultWaitTimeout,
            LeaseDuration = options?.LeaseDuration
        };
    }
}
