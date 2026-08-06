using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption> AddDiffHighlightUI(
            Action<ModuleDiffHighlightUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption>(action);
            registration.Require<ModuleDiffHighlight, ModuleDiffHighlightOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<SharedResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<DiffHighlightPage, SharedResource>(
                    DiffHighlightPage.DIFF_HIGHLIGHT_URL,
                    "Pages:DiffHighlight:Title",
                    Icons.Material.Filled.Compare,
                    BuiltInNavigationCategoryIds.Debug,
                    addToNav: true,
                    navOrder: 60));
            return registration;
        }
    }
}

/// <summary>
/// Text difference contrast highlighting UI module
/// </summary>
public class ModuleDiffHighlightUI : MonicaModule<ModuleDiffHighlightUIOption>, IUIModule
{
    public override void ConfigureServices(ModuleContext<ModuleDiffHighlightUIOption> context)
    {
        // The mixed module already registers the diff facade and infrastructure services.
    }
}

/// <summary>
/// DiffHighlightUI module options
/// </summary>
public class ModuleDiffHighlightUIOption : ModuleOptions<ModuleDiffHighlightUI>
{
}
