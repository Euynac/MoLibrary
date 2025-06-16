using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Framework.Modules;


public static class ModuleProfilingBuilderExtensions
{
    public static ModuleProfilingGuide ConfigModuleProfiling(this WebApplicationBuilder builder,
        Action<ModuleProfilingOption>? action = null)
    {
        return new ModuleProfilingGuide().Register(action);
    }
}

public class ModuleProfiling(ModuleProfilingOption option)
    : MoModule<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Profiling;
    }
}

public class ModuleProfilingGuide : MoModuleGuide<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>
{


}

public class ModuleProfilingOption : MoModuleOption<ModuleProfiling>
{
}