using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DevOps.FileOps.Pages;
using Monica.DevOps.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(BuiltInModuleKey.FileOpsUI)]
public class ModuleFileOpsUI(ModuleFileOpsUIOption option)
    : ModuleBase<ModuleFileOpsUI, ModuleFileOpsUIOption, ModuleFileOpsUIGuide>(option)
{
    public override void ClaimDependencies()
    {
        if (Option.DisableFileOpsPage)
        {
            return;
        }

        DependsOnModule<ModuleFileOpsGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIFileOpsPage, FileOpsResource>(
                UIFileOpsPage.PAGE_URL,
                "Pages:FileOps:Title",
                Icons.Material.Filled.Folder,
                BuiltInNavigationCategoryIds.Infrastructure,
                addToNav: true,
                navOrder: 36));
    }
}

public static class ModuleFileOpsUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        public ModuleFileOpsUIGuide AddFileOpsUI(Action<ModuleFileOpsUIOption>? action = null)
        {
            return builder.AddModule<ModuleFileOpsUI, ModuleFileOpsUIOption, ModuleFileOpsUIGuide>(action);
        }
    }
}

public class ModuleFileOpsUIGuide : ModuleGuide<ModuleFileOpsUI, ModuleFileOpsUIOption, ModuleFileOpsUIGuide>
{
}

public class ModuleFileOpsUIOption : ModuleOptions<ModuleFileOpsUI>
{
    public bool DisableFileOpsPage { get; set; }
}
