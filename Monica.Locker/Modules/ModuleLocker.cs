using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Locker.Abstractions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Locker.Providers.InProcess;
using Monica.Locker.Providers.Medallion;
using Monica.Locker.Services;
using MedallionDistributedLockProvider = Medallion.Threading.IDistributedLockProvider;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLockerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Locker module
        /// </summary>
        public static ModuleLockerGuide AddLocker(Action<ModuleLockerOption>? action = null)
        {
            return new ModuleLockerGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Locker)]
public class ModuleLocker(ModuleLockerOption option) : MoModule<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<LockKeyNormalizer>();
        services.AddSingleton<IDistributedLock, DistributedLockService>();
    }
}

public class ModuleLockerGuide : MoModuleGuide<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(UseProvider)];
    }

    public ModuleLockerGuide UseProvider<TProvider>() where TProvider : class, ILockProvider
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<ILockProvider, TProvider>();
        });

        return this;
    }

    /// <summary>
    /// Adds the Medallion distributed locking provider to the service collection.
    /// </summary>
    /// <param name="distributedLockProvider">The Medallion distributed lock provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public ModuleLockerGuide UseMedallionProvider(
        MedallionDistributedLockProvider distributedLockProvider)
    {
        ArgumentNullException.ThrowIfNull(distributedLockProvider);

        UseProvider<MedallionLockProvider>();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton(distributedLockProvider);
        });

        return this;
    }

    /// <summary>
    /// Adds the local distributed locking provider to the service collection.
    /// </summary>
    /// <returns>The service collection for chaining.</returns>
    public ModuleLockerGuide UseInProcessProvider()
    {
        return UseProvider<InProcessLockProvider>();
    }
}

public class ModuleLockerOption : MoModuleOption<ModuleLocker>
{
    /// <summary>
    /// Lock key prefix used when normalizing logical resource names.
    /// </summary>
    public string LockKeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Default time to wait when acquiring a lock before returning null.
    /// </summary>
    public TimeSpan DefaultWaitTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
