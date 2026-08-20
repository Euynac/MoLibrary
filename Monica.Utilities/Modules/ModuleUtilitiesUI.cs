using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
public class ModuleUtilitiesUI : MonicaModule<ModuleUtilitiesUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleUtilities, ModuleUtilitiesOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<UtilitiesResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIUtilitiesPage, UtilitiesResource>(
                    UIUtilitiesPage.PAGE_URL,
                    "Pages:UtilitiesToolbox:Title",
                    Icons.Material.Filled.Build,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 38)));
    }

    /// <summary>
    /// Registers page-local UI state used by the toolbox panels.
    /// </summary>
    public override void ConfigureServices(ModuleContext<ModuleUtilitiesUIOption> context)
    {
        context.Services.AddScoped<ConnectivityProbeState>();
        context.Services.AddScoped<TextTransformState>();
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
        /// <returns>The host-bound utilities UI registration.</returns>
        public ModuleRegistration<ModuleUtilitiesUI, ModuleUtilitiesUIOption> AddUtilitiesUI(
            Action<ModuleUtilitiesUIOption>? action = null)
        {
            return builder.AddModule<ModuleUtilitiesUI, ModuleUtilitiesUIOption>(action);
        }
    }
}

/// <summary>
/// Options for the utilities toolbox UI module.
/// </summary>
public class ModuleUtilitiesUIOption : ModuleOptions<ModuleUtilitiesUI>
{
}
