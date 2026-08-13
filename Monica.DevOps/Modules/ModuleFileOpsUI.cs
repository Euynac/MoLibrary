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
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<FileOpsResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIFileOpsPage, FileOpsResource>(
                    UIFileOpsPage.PAGE_URL,
                    "Pages:FileOps:Title",
                    Icons.Material.Filled.Folder,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 36)));
    }
}

public static class ModuleFileOpsUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        public ModuleRegistration<ModuleFileOpsUI, ModuleFileOpsUIOption> AddFileOpsUI(Action<ModuleFileOpsUIOption>? action = null)
        {
            return builder.AddModule<ModuleFileOpsUI, ModuleFileOpsUIOption>(action);
        }
    }
}



public class ModuleFileOpsUIOption : ModuleOptions<ModuleFileOpsUI>;
