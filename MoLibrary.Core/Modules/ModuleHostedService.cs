using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.HostedServices;
using MoLibrary.Core.HostedServices.Extensions;
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

        // Process any pending registration actions after all services are registered
        services.AddSingleton(provider =>
        {
            var actions = provider.GetServices<MoHostedServiceExtensions.IHostedServiceRegistrationAction>();
            foreach (var action in actions)
            {
                action.Execute(provider);
            }
            return new HostedServiceRegistrationProcessor();
        });
    }

    /// <summary>
    /// Marker class to ensure registration actions are executed
    /// </summary>
    private class HostedServiceRegistrationProcessor
    {
    }
}
