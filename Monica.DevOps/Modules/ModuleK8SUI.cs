using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.DevOps.K8S.Pages;
using Monica.DevOps.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public class ModuleK8SUI : MonicaModule<ModuleK8SUIOption>, IUIModule
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleK8S, ModuleK8SOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<K8SResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIK8SPage, K8SResource>(
                    UIK8SPage.PAGE_URL,
                    "Pages:K8S:Title",
                    Icons.Material.Filled.Dns,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 35)));
    }
}

public static class ModuleK8SUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        public ModuleRegistration<ModuleK8SUI, ModuleK8SUIOption> AddK8SUI(Action<ModuleK8SUIOption>? action = null)
        {
            return builder.AddModule<ModuleK8SUI, ModuleK8SUIOption>(action);
        }
    }
}



public class ModuleK8SUIOption : ModuleOptions<ModuleK8SUI>;
