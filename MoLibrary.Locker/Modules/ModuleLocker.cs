using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Medallion.Threading;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Local;
using MoLibrary.Locker.Providers.Medallion;

namespace MoLibrary.Locker.Modules;

public static class ModuleLockerBuilderExtensions
{
    public static ModuleLockerGuide ConfigModuleLocker(this WebApplicationBuilder builder,
        Action<ModuleLockerOption>? action = null)
    {
        return new ModuleLockerGuide().Register(action);
    }
}

public class ModuleLocker(ModuleLockerOption option) : MoModule<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Locker;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDistributedLockKeyNormalizer, DistributedLockKeyNormalizer>();
    }
}

public class ModuleLockerGuide : MoModuleGuide<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetDistributedLockProvider)];
    }

    public ModuleLockerGuide SetDistributedLockProvider<TProvider>() where TProvider : class, IMoDistributedLock
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoDistributedLock, TProvider>();
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
        ConfigureServices(context =>
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
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoDistributedLock, LocalMoDistributedLock>();
        });
        return this;
    }
}

public class ModuleLockerOption : MoModuleOption<ModuleLocker>
{
    /// <summary>
    /// DistributedLock key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "";
}
