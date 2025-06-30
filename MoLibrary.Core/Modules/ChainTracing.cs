using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;


public static class ModuleChainTracingBuilderExtensions
{
    public static ModuleChainTracingGuide ConfigModuleChainTracing(this WebApplicationBuilder builder,
        Action<ModuleChainTracingOption>? action = null)
    {
        return new ModuleChainTracingGuide().Register(action);
    }
}

public class ModuleChainTracing(ModuleChainTracingOption option)
    : MoModule<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ChainTracing;
    }
}

public class
    ModuleChainTracingGuide : MoModuleGuide<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>
{


}

public class ModuleChainTracingOption : MoModuleOption<ModuleChainTracing>
{
}