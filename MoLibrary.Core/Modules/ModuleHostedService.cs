using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.HostedServices;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// HostedService 可观测性模块
/// 提供统一的 HostedService 状态管理、异常池集成、心跳监控和集中管理功能
/// </summary>
public class ModuleHostedService(ModuleHostedServiceOption option)
    : MoModuleWithDependencies<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.HostedService;
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
