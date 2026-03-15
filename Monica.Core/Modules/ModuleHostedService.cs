using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Features.HostedServices;
using Monica.Core.Features.HostedServices.Interfaces;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

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
public class ModuleHostedService(ModuleHostedServiceOption option)
    : MoModuleWithDependencies<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.HostedService;
    }

    public override void ClaimDependencies()
    {
        // Depend on ObservableInstance module for state and exception tracking
        DependsOnModule<ModuleObservableInstanceGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MoHostedServiceManager>();
        services.AddSingleton<IMoHostedServiceManager>(provider => provider.GetRequiredService<MoHostedServiceManager>());
        services.AddSingleton<IMoHostedServiceDependencyCoordinator>(provider => provider.GetRequiredService<MoHostedServiceManager>());
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        Task.Run(() =>
        {
            try
            {
                // Register all IHostedService instances that implement IMoHostedService
                var manager = app.ApplicationServices.GetRequiredService<IMoHostedServiceManager>();
                var hostedServices = app.ApplicationServices.GetServices<IHostedService>();

                foreach (var service in hostedServices)
                {
                    if (service is not IMoHostedService moHostedService) continue;

                    // Register with manager
                    manager.RegisterService(moHostedService);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error registering MoHostedServices");
            }
        });
    }
}

/// <summary>
/// Fluent configuration guide for the HostedService observability module
/// </summary>
public class ModuleHostedServiceGuide
    : MoModuleGuide<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>
{
}

/// <summary>
/// Configuration options for the HostedService observability module
/// </summary>
public class ModuleHostedServiceOption : MoModuleOption<ModuleHostedService>
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
