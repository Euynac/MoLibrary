using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Services;
using Monica.Core.HostedService.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleHostedServiceBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the HostedService module.
        /// </summary>
        public static ModuleHostedServiceGuide AddHostedService(Action<ModuleHostedServiceOption>? action = null)
        {
            return new ModuleHostedServiceGuide().Register(action);
        }
    }
}

/// <summary>
/// HostedService observability module.
/// Provides centralized HostedService state management, heartbeat monitoring, and coordination support.
/// </summary>
[ModuleKey(BuiltInModuleKey.HostedService)]
public class ModuleHostedService(ModuleHostedServiceOption option)
    : ModuleBase<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>(option)
{

    public override void ClaimDependencies()
    {
        // Depend on ObservableInstance module for state and exception tracking
        DependsOnModule<ModuleObservableInstanceGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HostedServiceRegistry>();
        services.AddSingleton<IHostedServiceRegistryWriter>(provider => provider.GetRequiredService<HostedServiceRegistry>());
        services.AddSingleton<IMoHostedServiceRegistry>(provider => provider.GetRequiredService<HostedServiceRegistry>());
        services.AddSingleton<HostedServiceCheckpointCoordinator>();
        services.AddSingleton<IHostedServiceCheckpointObserver>(provider => provider.GetRequiredService<HostedServiceCheckpointCoordinator>());
        services.AddSingleton<IMoHostedServiceCheckpointCoordinator>(provider => provider.GetRequiredService<HostedServiceCheckpointCoordinator>());
        services.AddSingleton<HostedServiceRegistryInitializer>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.ApplicationServices
            .GetRequiredService<HostedServiceRegistryInitializer>()
            .Initialize(app.ApplicationServices);
    }
}

/// <summary>
/// Fluent configuration guide for the HostedService observability module
/// </summary>
public class ModuleHostedServiceGuide
    : ModuleGuide<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>
{
}

/// <summary>
/// Configuration options for the HostedService observability module
/// </summary>
public class ModuleHostedServiceOption : ModuleOptions<ModuleHostedService>
{
    /// <summary>
    /// Gets or sets the default maximum history size for all services
    /// </summary>
    public int DefaultMaxHistorySize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default heartbeat interval for BackgroundServices
    /// </summary>
    public TimeSpan DefaultHeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether to fail fast (throw exception) when StartAsync encounters an error.
    /// When true, exceptions during service startup will be re-thrown, causing the application to fail fast.
    /// When false, exceptions are logged but not re-thrown, allowing the application to continue.
    /// Default is false for production stability.
    /// </summary>
    public bool FailFastOnStartupError { get; set; } = false;
}
