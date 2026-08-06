using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.DevOps.FileOps.Pages;
using Monica.DevOps.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public class ModuleFileOpsUI : MonicaModule<ModuleFileOpsUIOption>, IUIModule
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleFileOps, ModuleFileOpsOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
    }
}

public static class ModuleFileOpsUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        public ModuleRegistration<ModuleFileOpsUI, ModuleFileOpsUIOption> AddFileOpsUI(Action<ModuleFileOpsUIOption>? action = null)
        {
            var module = builder.AddModule<ModuleFileOpsUI, ModuleFileOpsUIOption>(action);
            module.Require<ModuleLocalization, ModuleLocalizationOption>().AddResource<FileOpsResource>();
            module.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIFileOpsPage, FileOpsResource>(
                    UIFileOpsPage.PAGE_URL,
                    "Pages:FileOps:Title",
                    Icons.Material.Filled.Folder,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 36));
            return module;
        }
    }
}



public class ModuleFileOpsUIOption : ModuleOptions<ModuleFileOpsUI>;
