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
        /// Configure the FrameworkUI module
        /// </summary>
        public static ModuleFrameworkUIGuide AddFrameworkUI(Action<ModuleFrameworkUIOption>? action = null)
        {
            return new ModuleFrameworkUIGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.FrameworkUI)]
public class ModuleFrameworkUI(ModuleFrameworkUIOption option)
    : MoModule<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        // Depends on UIStackTrace module (for stack trace visualization)
        DependsOnModule<ModuleUIStackTraceGuide>().Register();
    }
}

public class ModuleFrameworkUIGuide : MoModuleGuide<ModuleFrameworkUI, ModuleFrameworkUIOption, ModuleFrameworkUIGuide>
{

}

public class ModuleFrameworkUIOption : MoModuleOption<ModuleFrameworkUI>
{

}