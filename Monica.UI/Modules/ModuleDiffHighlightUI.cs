using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDiffHighlightUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the DiffHighlightUI module
        /// </summary>
        public ModuleDiffHighlightUIGuide AddDiffHighlightUI(Action<ModuleDiffHighlightUIOption>? action = null)
        {
            return builder.AddModule<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>(action);
        }
    }
}

/// <summary>
/// Text difference contrast highlighting UI module
/// </summary>
[ModuleKey(BuiltInModuleKey.DiffHighlightUI)]
public class ModuleDiffHighlightUI(ModuleDiffHighlightUIOption option)
    : ModuleBase<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>(option)
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
                .RegisterUIComponents(p => p.RegisterLocalizedPage<DiffHighlightPage, SharedResource>(
                    DiffHighlightPage.DIFF_HIGHLIGHT_URL,
                    "Pages:DiffHighlight:Title",
                    Icons.Material.Filled.Compare,
                    BuiltInNavigationCategoryIds.Debug,
                    addToNav: true,
                    navOrder: 60));
        }
    }
}

/// <summary>
/// DiffHighlightUI Module Wizard
/// </summary>
public class ModuleDiffHighlightUIGuide : ModuleGuide<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>
{
}

/// <summary>
/// DiffHighlightUI module options
/// </summary>
public class ModuleDiffHighlightUIOption : ModuleOptions<ModuleDiffHighlightUI>
{ 
    /// <summary>
    /// Whether to disable the difference comparison page
    /// </summary>
    public bool DisableDiffHighlightPage { get; set; }
}
