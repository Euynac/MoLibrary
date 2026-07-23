using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Shell.Models;
using Monica.Utilities.Localization;
using Monica.Utilities.Pages;
using Monica.Utilities.UIUtilities.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Registers the utilities toolbox page inside the Monica shell.
/// </summary>
[ModuleKey(BuiltInModuleKey.UtilitiesUI)]
public class ModuleUtilitiesUI(ModuleUtilitiesUIOption option)
    : ModuleBase<ModuleUtilitiesUI, ModuleUtilitiesUIOption, ModuleUtilitiesUIGuide>(option)
{
    /// <summary>
    /// Registers page-local UI state used by the toolbox panels.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ConnectivityProbeState>();
        services.AddScoped<TextTransformState>();
    }

    /// <summary>
    /// Declares the utilities dependencies and the navigation entry for the toolbox page.
    /// </summary>
    public override void ClaimDependencies()
    {
        if (Option.DisableUtilitiesPage)
        {
            return;
        }

        DependsOnModule<ModuleUtilitiesGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIUtilitiesPage, UtilitiesResource>(
                UIUtilitiesPage.PAGE_URL,
                "Pages:UtilitiesToolbox:Title",
                Icons.Material.Filled.Build,
                BuiltInNavigationCategoryIds.Infrastructure,
                addToNav: true,
                navOrder: 38));
    }
}

/// <summary>
/// Builder extensions for the utilities UI module.
/// </summary>
public static class ModuleUtilitiesUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the utilities toolbox UI module.
        /// </summary>
        /// <param name="action">Optional configuration applied to <see cref="ModuleUtilitiesUIOption" />.</param>
        /// <returns>The fluent guide used to continue module configuration.</returns>
        public ModuleUtilitiesUIGuide AddUtilitiesUI(Action<ModuleUtilitiesUIOption>? action = null)
        {
            return builder.AddModule<ModuleUtilitiesUI, ModuleUtilitiesUIOption, ModuleUtilitiesUIGuide>(action);
        }
    }
}

/// <summary>
/// Fluent configuration guide for the utilities UI module.
/// </summary>
public class ModuleUtilitiesUIGuide : ModuleGuide<ModuleUtilitiesUI, ModuleUtilitiesUIOption, ModuleUtilitiesUIGuide>
{
}

/// <summary>
/// Options for the utilities toolbox UI module.
/// </summary>
public class ModuleUtilitiesUIOption : ModuleOptions<ModuleUtilitiesUI>
{
    /// <summary>
    /// Disables registration of the utilities toolbox page in the Monica shell.
    /// Default: <see langword="false" />.
    /// Set this when the backend utilities should remain available but the interactive page must stay hidden.
    /// </summary>
    public bool DisableUtilitiesPage { get; set; }
}
