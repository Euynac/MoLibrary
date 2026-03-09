using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Monica.Locker.DistributedLocking;
using Monica.Modules;
using Monica.Locker.Providers.Local;
using Monica.Locker.Providers.Medallion;

namespace Monica.Locker;

public static class ServicesCollectionExtensions
{
    /// <summary>
    /// Adds the core distributed locking services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the distributed locking options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMoDistributedLocking(
        this IServiceCollection services,
        Action<ModuleLockerOption>? configure = null)
    {
        services.Configure<ModuleLockerOption>(options =>
        {
            configure?.Invoke(options);
        });

        services.AddSingleton<IDistributedLockKeyNormalizer, DistributedLockKeyNormalizer>();

        return services;
    }

    /// <summary>
    /// Adds the Medallion distributed locking provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="distributedLockProvider">The Medallion distributed lock provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMedallionDistributedLock(
        this IServiceCollection services,
        IDistributedLockProvider distributedLockProvider)
    {
        services.AddSingleton(distributedLockProvider);
        services.AddSingleton<IMoDistributedLock, MedallionMoDistributedLock>();

        return services;
    }

    /// <summary>
    /// Adds the local distributed locking provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalDistributedLock(
        this IServiceCollection services)
    {
        services.AddSingleton<IMoDistributedLock, LocalMoDistributedLock>();

        return services;
    }
}