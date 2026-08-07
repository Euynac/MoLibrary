using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Cancellation.Abstractions;
using Monica.StateStore.Cancellation.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleCancellationManagerBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the CancellationManager module
        /// </summary>
        public ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> AddCancellationManager(Action<ModuleCancellationManagerOption>? action = null)
        {
            return builder.AddModule<ModuleCancellationManager, ModuleCancellationManagerOption>(action);
        }
    }
}

/// <summary>
/// Distributed cancellation token manager module
/// Provide cancellation token management capabilities across microservice instances
/// </summary>
public class ModuleCancellationManager : MonicaModule<ModuleCancellationManagerOption>
{
    /// <summary>
    /// Configure service dependency injection
    /// </summary>
    /// <param name="context">The module-owned service registration context.</param>
    public override void ConfigureServices(ModuleContext<ModuleCancellationManagerOption> context)
    {
        var services = context.Services;
        // Choose the appropriate implementation based on your configuration
        if (!Option.UseDistributed)
        {
            // Register the memory version to cancel the token manager service
            services.AddSingleton<ICancellationManager, InMemoryCancellationManager>();
        }
        else
        {
            // Register the distributed cancellation token manager service
            services.AddSingleton<ICancellationManager>((serviceProvider) =>
            {
                var stateStore = serviceProvider.GetRequiredKeyedService<IStateStore>(nameof(ModuleCancellationManager));
                return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
            });
        }
    }

    public override void Describe(ModuleDescriptor module)
    {
    }
}

/// <summary>
/// Registration extensions for the distributed cancellation-token manager module.
/// </summary>
public static class ModuleCancellationManagerRegistrationExtensions
{
    /// <summary>
    /// Enables the distributed cancellation implementation and its state-store dependency.
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> UseDistributedCancellation(
        this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module)
    {
        module.Configure(options => options.UseDistributed = true);
        module.Require<ModuleStateStore, ModuleStateStoreOption>()
            .AddKeyedCommonStateStore(nameof(ModuleCancellationManager), useDistributed: true);
        return module;
    }

    /// <summary>
    /// Adds a cancellation token manager for the specified key
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    /// <param name="key">service key</param>
    /// <param name="useDistributed">Whether to use memory implementation, the default is false</param>
    /// <returns>The current module registration for chaining.</returns>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> AddKeyedCancellationManager(this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module, string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            // Using distributed implementation, you need to rely on StateStore
            module.Require<ModuleStateStore, ModuleStateStoreOption>().AddKeyedCommonStateStore(key, true);
        }

        module.ConfigureServices(context =>
        {
            context.Services.AddKeyedSingleton<ICancellationManager>(key, (serviceProvider, _) =>
            {
                if (!useDistributed)
                {
                    // Use memory implementation
                    return ActivatorUtilities.CreateInstance<InMemoryCancellationManager>(serviceProvider);
                }
                else
                {
                    // Use distributed implementation
                    var stateStore = serviceProvider.GetRequiredKeyedService<IStateStore>(key);
                    return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
                }
            });
        });
        module.RecordKeyedServiceKey(key);
        return module;
    }


}

/// <summary>
/// Distributed cancellation token manager module configuration options
/// </summary>
public class ModuleCancellationManagerOption : ModuleOptions<ModuleCancellationManager>
{
    /// <summary>
    /// Gets whether the distributed implementation was selected through
    /// <see cref="ModuleCancellationManagerRegistrationExtensions.UseDistributedCancellation"/>.
    /// The default uses the in-memory implementation.
    /// </summary>
    public bool UseDistributed { get; internal set; }

    /// <summary>
    /// Polling interval (milliseconds), default is 1000ms
    /// Only valid when using distributed implementation
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether to enable detailed logging, the default is false
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// TTL (time to live) for canceling token status, default is 24 hours
    /// Set to null to never expire
    /// Only valid when using distributed implementation
    /// </summary>
    public TimeSpan? StateTtl { get; set; } = TimeSpan.FromHours(24);

}
