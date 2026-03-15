using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleFrameworkChainTracingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 FrameworkChainTracing 模块
        /// </summary>
        public static ModuleFrameworkChainTracingGuide AddFrameworkChainTracing(Action<ModuleFrameworkChainTracingOption>? action = null)
        {
            return new ModuleFrameworkChainTracingGuide().Register(action);
        }
    }
}

public class ModuleFrameworkChainTracing(ModuleFrameworkChainTracingOption option)
    : MoModuleWithDependencies<ModuleFrameworkChainTracing, ModuleFrameworkChainTracingOption, ModuleFrameworkChainTracingGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.FrameworkChainTracing;
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
            //services.Decorate<IMoStateStore, ChainTrackingProviderIMoStateStoreDecorator>();
            //services.Decorate<IDistributedStateStore, ChainTrackingProviderIDistributedStateStoreDecorator>();
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