using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Dapr;
using MoLibrary.Locker.Providers.Local;
using MoLibrary.Locker.Providers.Medallion;

namespace MoLibrary.Locker.Modules;

public class ModuleLockerGuide : MoModuleGuide<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetDistributedLockProvider)];
    }

    protected ModuleLockerGuide SetDistributedLockProvider<TProvider>() where TProvider : class, IMoDistributedLock
    {
        ConfigureServices(nameof(SetDistributedLockProvider), context =>
        {
            context.Services.AddSingleton<IMoDistributedLock, TProvider>();
        });
        return this;
    }
    /// <summary>
    /// Adds the Dapr distributed locking provider to the service collection.
    /// </summary>
    /// <param name="configure">Action to configure the Dapr options.</param>
    /// <returns>The service collection for chaining.</returns>
    public ModuleLockerGuide AddDaprDistributedLock(
        Action<MoDistributedLockDaprOptions> configure)
    {
        ConfigureServices(nameof(SetDistributedLockProvider), context =>
        {
            context.Services.AddSingleton<IMoDistributedLock, DaprMoDistributedLock>();
            context.Services.Configure(configure);
        });
        return this;
    }

    /// <summary>
    /// Adds the Medallion distributed locking provider to the service collection.
    /// </summary>
    /// <param name="distributedLockProvider">The Medallion distributed lock provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public ModuleLockerGuide AddMedallionDistributedLock(
        IDistributedLockProvider distributedLockProvider)
    {
        ConfigureServices(nameof(SetDistributedLockProvider), context =>
        {
            context.Services.AddSingleton(distributedLockProvider);
            context.Services.AddSingleton<IMoDistributedLock, MedallionMoDistributedLock>();
        });
        return this;
    }

    /// <summary>
    /// Adds the local distributed locking provider to the service collection.
    /// </summary>
    /// <returns>The service collection for chaining.</returns>
    public ModuleLockerGuide AddLocalDistributedLock()
    {
        ConfigureServices(nameof(SetDistributedLockProvider), context =>
        {
            context.Services.AddSingleton<IMoDistributedLock, LocalMoDistributedLock>();
        });
        return this;
    }
}