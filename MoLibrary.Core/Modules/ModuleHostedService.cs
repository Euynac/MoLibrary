using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Extension methods for configuring the HostedService observability module and registering observable hosted services
/// </summary>
public static class ModuleHostedServiceBuilderExtensions
{
    /// <summary>
    /// Configures the HostedService observability module
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <param name="action">Optional configuration action</param>
    /// <returns>The module guide for fluent configuration</returns>
    public static ModuleHostedServiceGuide ConfigModuleHostedService(
        this WebApplicationBuilder builder,
        Action<ModuleHostedServiceOption>? action = null)
    {
        return new ModuleHostedServiceGuide().Register(action);
    }
}

/// <summary>
/// HostedService 可观测性模块
/// 提供统一的 HostedService 状态管理、异常池集成、心跳监控和集中管理功能
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
        // Register the HostedService manager as singleton
        services.AddSingleton<IMoHostedServiceManager, MoHostedServiceManager>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        // Register all IHostedService instances that implement IMoHostedService
        var manager = app.ApplicationServices.GetRequiredService<IMoHostedServiceManager>();
        var hostedServices = app.ApplicationServices.GetServices<IHostedService>();

        foreach (var service in hostedServices)
        {
            if (service is not IMoHostedService moHostedService) continue;
            switch (service)
            {
                // Initialize observable info
                case MoHostedService moHosted:
                    moHosted.InitializeObservableInfo();
                    break;
                case MoBackgroundService moBackground:
                    moBackground.InitializeObservableInfo();
                    break;
            }

            // Register with manager
            manager.RegisterService(moHostedService);
        }
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
