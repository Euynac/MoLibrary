using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.DependencyInjection.Modules;

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
    }
}

public class ModuleFrameworkChainTracingGuide : MoModuleGuide<ModuleFrameworkChainTracing,
    ModuleFrameworkChainTracingOption, ModuleFrameworkChainTracingGuide>
{


}

public class ModuleFrameworkChainTracingOption : MoModuleOption<ModuleFrameworkChainTracing>
{
}