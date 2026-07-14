using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Locker.Abstractions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Locker.Models;
using Monica.Locker.Providers.InProcess;
using Monica.Locker.Providers.Medallion;
using Monica.Locker.Services;
using MedallionDistributedLockProvider = Medallion.Threading.IDistributedLockProvider;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLockerBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the Locker module and returns its guide for provider configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The locker guide used to select a concrete lock provider.</returns>
        public ModuleLockerGuide AddLocker(Action<ModuleLockerOption>? action = null)
        {
            return builder.AddModule<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>(action);
        }
    }
}

/// <summary>
/// Registers the shared locker services and exposes <see cref="IDistributedLock"/> to application code.
/// </summary>
[ModuleKey(BuiltInModuleKey.Locker)]
public class ModuleLocker(ModuleLockerOption option) : ModuleBase<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<LockKeyNormalizer>();
        services.AddSingleton<IDistributedLock, DistributedLockService>();
    }
}

/// <summary>
/// Configures which lock provider backs the Locker module.
/// </summary>
public class ModuleLockerGuide : ModuleGuide<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(UseProvider)];
    }

    /// <summary>
    /// Registers a custom <see cref="ILockProvider"/> implementation.
    /// </summary>
    /// <typeparam name="TProvider">The provider type used to acquire and release locks.</typeparam>
    /// <returns>The current guide instance.</returns>
    public ModuleLockerGuide UseProvider<TProvider>() where TProvider : class, ILockProvider
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<ILockProvider, TProvider>();
        });

        return this;
    }

    /// <summary>
    /// Registers the Medallion-based distributed lock provider.
    /// </summary>
    /// <param name="distributedLockProvider">The Medallion distributed lock provider to use.</param>
    /// <returns>The current guide instance.</returns>
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
    /// Registers the in-process lock provider for single-process scenarios and local development.
    /// </summary>
    /// <returns>The current guide instance.</returns>
    public ModuleLockerGuide UseInProcessProvider()
    {
        return UseProvider<InProcessLockProvider>();
    }
}

/// <summary>
/// Configures cross-provider locker defaults.
/// </summary>
public class ModuleLockerOption : ModuleOptions<ModuleLocker>
{
    /// <summary>
    /// Prefix applied to every logical lock name before it reaches the active provider.
    /// </summary>
    public string LockKeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Default time to wait for a lock when a caller does not provide <see cref="LockAcquisitionOptions.WaitTimeout"/>.
    /// </summary>
    public TimeSpan DefaultWaitTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
