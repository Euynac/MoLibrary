using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDiffHighlightUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the DiffHighlightUI module
        /// </summary>
        public static ModuleDiffHighlightUIGuide AddDiffHighlightUI(Action<ModuleDiffHighlightUIOption>? action = null)
        {
            return new ModuleDiffHighlightUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Text difference contrast highlighting UI module
/// </summary>
[ModuleKey(EMoModuleKey.DiffHighlightUI)]
public class ModuleDiffHighlightUI(ModuleDiffHighlightUIOption option)
    : MoModule<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // The mixed module already registers the diff facade and infrastructure services.
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableDiffHighlightPage)
        {
            DependsOnModule<ModuleDiffHighlightGuide>().Register();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<DiffHighlightPage>(
                    DiffHighlightPage.DIFF_HIGHLIGHT_URL,
                    "Pages:DiffHighlight:Title",
                    Icons.Material.Filled.Compare,
                    "Categories:Debug",
                    addToNav: true,
                    navOrder: 60));
        }
    }
}

/// <summary>
/// DiffHighlightUI Module Wizard
/// </summary>
public class ModuleDiffHighlightUIGuide : MoModuleGuide<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>
{
}

/// <summary>
/// DiffHighlightUI module options
/// </summary>
public class ModuleDiffHighlightUIOption : MoModuleOption<ModuleDiffHighlightUI>
{ 
    /// <summary>
    /// Whether to disable the difference comparison page
    /// </summary>
    public bool DisableDiffHighlightPage { get; set; }
}
