using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.Modules;

namespace MoLibrary.Framework.UI.Modules;


public static class ModuleFrameworkUIBuilderExtensions
{
    public static ModuleFrameworkUIGuide ConfigModuleFrameworkUI(this WebApplicationBuilder builder,
        Action<ModuleFrameworkUIOption>? action = null)
    {
        return new ModuleFrameworkUIGuide().Register(action);
    }
}

public class ModuleFrameworkUI(ModuleFrameworkUIOption option)
    : MoModuleWithDependencies<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.FrameworkUI;
    }

    public override void ClaimDependencies()
    {
        // 依赖 UIStackTrace 模块（用于堆栈跟踪可视化）
        DependsOnModule<ModuleUIStackTraceGuide>().Register();
    }
}

public class ModuleFrameworkUIGuide : MoModuleGuide<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>
{


}

public class ModuleFrameworkUIOption : MoModuleOption<ModuleFrameworkUI>
{

}