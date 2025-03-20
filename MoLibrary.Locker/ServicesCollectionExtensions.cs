using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Dapr;
using MoLibrary.Locker.Providers.Local;
using MoLibrary.Locker.Providers.Medallion;

namespace MoLibrary.Locker;

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
        Action<MoDistributedLockOptions>? configure = null)
    {
        services.Configure<MoDistributedLockOptions>(options =>
        {
            configure?.Invoke(options);
        });

        services.AddSingleton<IDistributedLockKeyNormalizer, DistributedLockKeyNormalizer>();

        return services;
    }

    /// <summary>
    /// Adds the Dapr distributed locking provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the Dapr options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDaprDistributedLock(
        this IServiceCollection services,
        Action<MoDistributedLockDaprOptions> configure)
    {
        services.Configure<MoDistributedLockDaprOptions>(configure);
        services.AddSingleton<IMoDistributedLock, DaprMoDistributedLock>();

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