using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DevOps.K8S.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.K8SUI)]
public class ModuleK8SUI(ModuleK8SUIOption option)
    : MoModule<ModuleK8SUI, ModuleK8SUIOption, ModuleK8SUIGuide>(option)
{
    public override void ClaimDependencies()
    {
        if (Option.DisableK8SPage)
        {
            return;
        }

        DependsOnModule<ModuleK8SGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIK8SPage>(
                UIK8SPage.PAGE_URL,
                "Pages:K8S:Title",
                Icons.Material.Filled.Dns,
                "Categories:Infrastructure",
                addToNav: true,
                navOrder: 35));
    }
}

public static class ModuleK8SUIBuilderExtensions
{
    extension(Mo)
    {
        public static ModuleK8SUIGuide AddK8SUI(Action<ModuleK8SUIOption>? action = null)
        {
            return new ModuleK8SUIGuide().Register(action);
        }
    }
}

public class ModuleK8SUIGuide : MoModuleGuide<ModuleK8SUI, ModuleK8SUIOption, ModuleK8SUIGuide>
{
}

public class ModuleK8SUIOption : MoModuleOption<ModuleK8SUI>
{
    public bool DisableK8SPage { get; set; }
}
