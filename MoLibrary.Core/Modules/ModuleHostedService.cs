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
        // Depend on ExceptionPool module for exception tracking
        DependsOnModule<ModuleExceptionPoolGuide>().Register();
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
            if (service is IMoHostedService moHostedService)
            {
                // Initialize observable info
                if (service is MoHostedService moHosted)
                {
                    moHosted.InitializeObservableInfo();
                }
                else if (service is MoBackgroundService moBackground)
                {
                    moBackground.InitializeObservableInfo();
                }

                // Register with manager
                manager.RegisterService(moHostedService);
            }
        }
    }
}
