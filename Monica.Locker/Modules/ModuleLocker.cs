using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Locker.Abstractions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        /// Registers the Locker module and returns its host-bound provider configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The locker registration used to select a concrete lock provider.</returns>
        public ModuleRegistration<ModuleLocker, ModuleLockerOption> AddLocker(
            Action<ModuleLockerOption>? action = null)
        {
            return builder.AddModule<ModuleLocker, ModuleLockerOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleLocker, ModuleLockerOption> registration)
    {
        public ModuleRegistration<ModuleLocker, ModuleLockerOption> UseProvider<TProvider>()
            where TProvider : class, ILockProvider
        {
            return registration
                .ConfigureServices(context => context.Services.AddSingleton<ILockProvider, TProvider>())
                .SatisfyFeature(ModuleLocker.PROVIDER_FEATURE);
        }

        public ModuleRegistration<ModuleLocker, ModuleLockerOption> UseMedallionProvider(
            MedallionDistributedLockProvider distributedLockProvider)
        {
            ArgumentNullException.ThrowIfNull(distributedLockProvider);
            return registration
                .UseProvider<MedallionLockProvider>()
                .ConfigureServices(context => context.Services.AddSingleton(distributedLockProvider));
        }

        public ModuleRegistration<ModuleLocker, ModuleLockerOption> UseInProcessProvider()
        {
            return registration.UseProvider<InProcessLockProvider>();
        }
    }
}

/// <summary>
/// Registers the shared locker services and exposes <see cref="IDistributedLock"/> to application code.
/// </summary>
public class ModuleLocker : MonicaModule<ModuleLockerOption>
{
    internal const string PROVIDER_FEATURE = "lock-provider";

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(PROVIDER_FEATURE);
    }

    public override void ConfigureServices(ModuleContext<ModuleLockerOption> context)
    {
        context.Services.AddSingleton<LockKeyNormalizer>();
        context.Services.AddSingleton<IDistributedLock, DistributedLockService>();
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
