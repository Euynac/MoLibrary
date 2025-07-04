using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoDecorator;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.DependencyInjection.Modules;
using MoLibrary.Framework.Features.FrameworkChainTracing;
using MoLibrary.StateStore;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.Framework.Modules;


public static class ModuleFrameworkChainTracingBuilderExtensions
{
    public static ModuleFrameworkChainTracingGuide ConfigModuleFrameworkChainTracing(this WebApplicationBuilder builder,
        Action<ModuleFrameworkChainTracingOption>? action = null)
    {
        return new ModuleFrameworkChainTracingGuide().Register(action);
    }
}

public class ModuleFrameworkChainTracing(ModuleFrameworkChainTracingOption option)
    : MoModuleWithDependencies<ModuleFrameworkChainTracing, ModuleFrameworkChainTracingOption, ModuleFrameworkChainTracingGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.FrameworkChainTracing;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleChainTracingGuide>().Register();
        DependsOnModule<ModuleDynamicProxyGuide>().Register();
        if(option.EnableStateStoreTracing)
        {
            DependsOnModule<ModuleStateStoreGuide>().Register();
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        if(option.EnableStateStoreTracing)
        {
            services.Decorate<IMoStateStore, ChainTrackingProviderIMoStateStoreDecorator>();
            services.Decorate<IDistributedStateStore, ChainTrackingProviderIDistributedStateStoreDecorator>();
        }
    }
}

public class ModuleFrameworkChainTracingGuide : MoModuleGuide<ModuleFrameworkChainTracing,
    ModuleFrameworkChainTracingOption, ModuleFrameworkChainTracingGuide>
{


}

public class ModuleFrameworkChainTracingOption : MoModuleOption<ModuleFrameworkChainTracing>
{
    
    /// <summary>
    /// 是否启用StateStore的调用链追踪
    /// </summary>
    public bool EnableStateStoreTracing { get; set; }

}