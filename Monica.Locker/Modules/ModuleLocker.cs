using Microsoft.Extensions.DependencyInjection;
using Medallion.Threading;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Locker.DistributedLocking;
using Monica.Locker.Providers.Local;
using Monica.Locker.Providers.Medallion;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLockerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Locker 模块
        /// </summary>
        public static ModuleLockerGuide AddLocker(Action<ModuleLockerOption>? action = null)
        {
            return new ModuleLockerGuide().Register(action);
        }
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
            context.Services.AddScoped<IMoDistributedLock, TProvider>();
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
