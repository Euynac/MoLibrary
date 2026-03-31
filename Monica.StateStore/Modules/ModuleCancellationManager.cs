using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.StateStore;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Cancellation.Abstractions;
using Monica.StateStore.Cancellation.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleCancellationManagerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the CancellationManager module
        /// </summary>
        public static ModuleCancellationManagerGuide AddCancellationManager(Action<ModuleCancellationManagerOption>? action = null)
        {
            return new ModuleCancellationManagerGuide().Register(action);
        }
    }
}

/// <summary>
/// Distributed cancellation token manager module
/// Provide cancellation token management capabilities across microservice instances
/// </summary>
[ModuleKey(EMoModuleKey.CancellationManager)]
public class ModuleCancellationManager(ModuleCancellationManagerOption option)
    : MoModule<ModuleCancellationManager, ModuleCancellationManagerOption, ModuleCancellationManagerGuide>(option)
{
    /// <summary>
    /// Configure service dependency injection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Return configuration results</returns>
    public override void ConfigureServices(IServiceCollection services)
    {
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

    public override void ClaimDependencies()
    {
        if (Option.UseDistributed)
        {
            DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedCommonStateStore(nameof(ModuleCancellationManager), true);
        }
    }
}

/// <summary>
/// Distributed Cancellation Token Manager Module Guide
/// </summary>
public class ModuleCancellationManagerGuide : MoModuleGuide<ModuleCancellationManager, ModuleCancellationManagerOption,
    ModuleCancellationManagerGuide>
{
    /// <summary>
    /// Adds a cancellation token manager for the specified key
    /// </summary>
    /// <param name="key">service key</param>
    /// <param name="useDistributed">Whether to use memory implementation, the default is false</param>
    /// <returns>Returns the current module guide instance to support chained calls</returns>
    public ModuleCancellationManagerGuide AddKeyedCancellationManager(string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            // Using distributed implementation, you need to rely on StateStore
            DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedCommonStateStore(key, true);
        }

        ConfigureServices(context =>
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
        }, secondKey: key);
        RecordKeyedServiceKey(key);
        return this;
    }

}

/// <summary>
/// Distributed cancellation token manager module configuration options
/// </summary>
public class ModuleCancellationManagerOption : MoModuleOption<ModuleCancellationManager>
{
    /// <summary>
    /// Whether to use memory implementation, the default is false (use distributed implementation)
    /// </summary>
    public bool UseDistributed { get; set; } = false;

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