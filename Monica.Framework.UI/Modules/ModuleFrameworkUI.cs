using Microsoft.AspNetCore.Builder;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.UI.Modules;

namespace Monica.Framework.UI.Modules;


public static class ModuleFrameworkUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 FrameworkUI 模块
        /// </summary>
        public static ModuleFrameworkUIGuide AddFrameworkUI(Action<ModuleFrameworkUIOption>? action = null)
        {
            return new ModuleFrameworkUIGuide().Register(action);
        }
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