using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DevOps.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.FileOpsUI)]
public class ModuleFileOpsUI(ModuleFileOpsUIOption option)
    : MoModule<ModuleFileOpsUI, ModuleFileOpsUIOption, ModuleFileOpsUIGuide>(option)
{
    public override void ClaimDependencies()
    {
        if (Option.DisableFileOpsPage)
        {
            return;
        }

        DependsOnModule<ModuleFileOpsGuide>().Register();
        DependsOnModule<ModuleUICoreGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIFileOpsPage>(
                UIFileOpsPage.PAGE_URL,
                "Pages:FileOps:Title",
                Icons.Material.Filled.Folder,
                "Categories:Infrastructure",
                addToNav: true,
                navOrder: 36));
    }
}

public static class ModuleFileOpsUIBuilderExtensions
{
    extension(Mo)
    {
        public static ModuleFileOpsUIGuide AddFileOpsUI(Action<ModuleFileOpsUIOption>? action = null)
        {
            return new ModuleFileOpsUIGuide().Register(action);
        }
    }
}

public class ModuleFileOpsUIGuide : MoModuleGuide<ModuleFileOpsUI, ModuleFileOpsUIOption, ModuleFileOpsUIGuide>
{
}

public class ModuleFileOpsUIOption : MoModuleOption<ModuleFileOpsUI>
{
    public bool DisableFileOpsPage { get; set; }
}
