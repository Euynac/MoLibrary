using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.FrameworkUI.Modules;


public static class ModuleFrameworkUIBuilderExtensions
{
    public static ModuleFrameworkUIGuide ConfigModuleFrameworkUI(this WebApplicationBuilder builder,
        Action<ModuleFrameworkUIOption>? action = null)
    {
        return new ModuleFrameworkUIGuide().Register(action);
    }
}

public class ModuleFrameworkUI(ModuleFrameworkUIOption option)
    : MoModule<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.FrameworkUI;
    }
}

public class ModuleFrameworkUIGuide : MoModuleGuide<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>
{


}

public class ModuleFrameworkUIOption : MoModuleOption<ModuleFrameworkUI>
{
  
}