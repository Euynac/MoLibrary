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
/// Registers process-local or distributed cancellation-token management.
/// Keyed consumers choose their provider explicitly without inheriting the module-wide default.
/// </summary>
public class ModuleCancellationManager : MonicaModule<ModuleCancellationManagerOption>
{
    /// <inheritdoc />
    public override void ValidateOptions(ModuleCancellationManagerOption options, string? profileName)
    {
        if (!Enum.IsDefined(options.Mode))
        {
            throw new InvalidOperationException(
                $"Unsupported {nameof(CancellationManagerMode)} value '{options.Mode}'.");
        }
    }

    /// <summary>
    /// Configure service dependency injection
    /// </summary>
    /// <param name="context">The module-owned service registration context.</param>
    public override void ConfigureServices(ModuleContext<ModuleCancellationManagerOption> context)
    {
        var services = context.Services;
        switch (Option.Mode)
        {
            case CancellationManagerMode.InMemory:
                services.AddSingleton<ICancellationManager, InMemoryCancellationManager>();
                break;
            case CancellationManagerMode.Distributed:
                services.AddSingleton<ICancellationManager>(serviceProvider =>
                {
                    var stateStore = serviceProvider.GetRequiredKeyedService<IStateStore>(nameof(ModuleCancellationManager));
                    return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
                });
                break;
            default:
                throw new InvalidOperationException("Cancellation manager mode validation did not run.");
        }
    }

    /// <inheritdoc />
    public override void DeclareContracts(ModuleContractDescriptor<ModuleCancellationManagerOption> contracts)
    {
        if (contracts.Options.Mode == CancellationManagerMode.Distributed)
        {
            contracts.RequireKeyedService<IStateStore>(nameof(ModuleCancellationManager));
        }
    }
}

/// <summary>
/// Registration extensions for cancellation-token providers.
/// </summary>
public static class ModuleCancellationManagerRegistrationExtensions
{
    /// <summary>
    /// Uses the process-local cancellation implementation. This is the default module-wide mode.
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> UseInMemoryCancellation(
        this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module)
    {
        return module.Configure(options => options.Mode = CancellationManagerMode.InMemory);
    }

    /// <summary>
    /// Enables the distributed cancellation implementation and its state-store dependency.
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> UseDistributedCancellation(
        this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module)
    {
        module.Configure(options => options.Mode = CancellationManagerMode.Distributed);
        module.Require<ModuleStateStore, ModuleStateStoreOption>()
            .AddKeyedCommonStateStore(nameof(ModuleCancellationManager), useDistributed: true);
        return module;
    }

    /// <summary>
    /// Adds a process-local cancellation manager for the specified key.
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    /// <param name="key">The keyed-service identifier.</param>
    /// <returns>The current module registration for chaining.</returns>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> AddKeyedInMemoryCancellationManager(
        this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        module.ConfigureServices(context =>
            context.Services.AddKeyedSingleton<ICancellationManager, InMemoryCancellationManager>(key));
        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Adds a distributed cancellation manager for the specified key and binds it to the distributed StateStore
    /// provider selected for the host.
    /// </summary>
    /// <param name="module">The CancellationManager registration being configured.</param>
    /// <param name="key">The keyed-service identifier.</param>
    /// <returns>The current module registration for chaining.</returns>
    public static ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> AddKeyedDistributedCancellationManager(
        this ModuleRegistration<ModuleCancellationManager, ModuleCancellationManagerOption> module,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        module.Require<ModuleStateStore, ModuleStateStoreOption>().AddKeyedCommonStateStore(key, true);
        module.ConfigureServices(context =>
        {
            context.Services.AddKeyedSingleton<ICancellationManager>(key, (serviceProvider, _) =>
            {
                var stateStore = serviceProvider.GetRequiredKeyedService<IStateStore>(key);
                return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
            });
        });
        module.RecordKeyedServiceKey(key);
        return module;
    }
}

/// <summary>
/// Configures cancellation-token manager behavior.
/// </summary>
public class ModuleCancellationManagerOption : ModuleOptions<ModuleCancellationManager>
{
    /// <summary>
    /// Gets the module-wide cancellation implementation. The default is
    /// <see cref="CancellationManagerMode.InMemory"/>; select distributed mode when cancellation must cross hosts.
    /// </summary>
    public CancellationManagerMode Mode { get; internal set; } = CancellationManagerMode.InMemory;

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

/// <summary>
/// Defines the persistence and propagation boundary of a cancellation manager.
/// </summary>
public enum CancellationManagerMode
{
    /// <summary>
    /// Keeps cancellation state inside the current process.
    /// </summary>
    InMemory,

    /// <summary>
    /// Persists cancellation state through the host's distributed StateStore provider.
    /// </summary>
    Distributed
}
