using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.DevOps.Localization;
using Monica.DevOps.Terminal.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the local terminal UI module.
/// </summary>
public static class ModuleTerminalUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the local terminal UI module and applies optional configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The host-bound terminal UI module registration.</returns>
        public ModuleRegistration<ModuleTerminalUI, ModuleTerminalUIOption> AddTerminalUI(Action<ModuleTerminalUIOption>? action = null)
        {
            var module = builder.AddModule<ModuleTerminalUI, ModuleTerminalUIOption>(action);
            module.Require<ModuleLocalization, ModuleLocalizationOption>().AddResource<TerminalResource>();
            module.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UITerminalPage, TerminalResource>(
                    UITerminalPage.PAGE_URL,
                    "Pages:Terminal:Title",
                    Icons.Material.Filled.Terminal,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 58));
            return module;
        }
    }
}

/// <summary>
/// Local terminal UI module.
/// </summary>
public sealed class ModuleTerminalUI : MonicaModule<ModuleTerminalUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleTerminal, ModuleTerminalOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
    }
}

/// <summary>
/// Registration extensions for the local terminal UI module.
/// </summary>


/// <summary>
/// Options for the local terminal UI module.
/// </summary>
public sealed class ModuleTerminalUIOption : ModuleOptions<ModuleTerminalUI>;
