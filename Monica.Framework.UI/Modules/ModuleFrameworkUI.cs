using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


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
    : MoModule<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>(option)
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