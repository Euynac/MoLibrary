using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DevOps.K8S.Pages;
using Monica.DevOps.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(BuiltInModuleKey.K8SUI)]
public class ModuleK8SUI(ModuleK8SUIOption option)
    : ModuleBase<ModuleK8SUI, ModuleK8SUIOption, ModuleK8SUIGuide>(option)
{
    public override void ClaimDependencies()
    {
        if (Option.DisableK8SPage)
        {
            return;
        }

        DependsOnModule<ModuleK8SGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIK8SPage, K8SResource>(
                UIK8SPage.PAGE_URL,
                "Pages:K8S:Title",
                Icons.Material.Filled.Dns,
                BuiltInNavigationCategoryIds.Infrastructure,
                addToNav: true,
                navOrder: 35));
    }
}

public static class ModuleK8SUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        public ModuleK8SUIGuide AddK8SUI(Action<ModuleK8SUIOption>? action = null)
        {
            return builder.AddModule<ModuleK8SUI, ModuleK8SUIOption, ModuleK8SUIGuide>(action);
        }
    }
}

public class ModuleK8SUIGuide : ModuleGuide<ModuleK8SUI, ModuleK8SUIOption, ModuleK8SUIGuide>
{
}

public class ModuleK8SUIOption : ModuleOptions<ModuleK8SUI>
{
    public bool DisableK8SPage { get; set; }
}
