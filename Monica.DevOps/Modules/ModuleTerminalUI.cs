using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DevOps.Localization;
using Monica.DevOps.Terminal.Pages;
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
        /// <returns>The terminal UI module guide.</returns>
        public ModuleTerminalUIGuide AddTerminalUI(Action<ModuleTerminalUIOption>? action = null)
        {
            return builder.AddModule<ModuleTerminalUI, ModuleTerminalUIOption, ModuleTerminalUIGuide>(action);
        }
    }
}

/// <summary>
/// Local terminal UI module.
/// </summary>
/// <param name="option">The terminal UI module options.</param>
[ModuleKey(BuiltInModuleKey.TerminalUI)]
public sealed class ModuleTerminalUI(ModuleTerminalUIOption option)
    : ModuleBase<ModuleTerminalUI, ModuleTerminalUIOption, ModuleTerminalUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleTerminalGuide>().Register();

        if (Option.DisableTerminalPage)
        {
            return;
        }

        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<TerminalResource>();

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UITerminalPage>(
                UITerminalPage.PAGE_URL,
                "Pages:Terminal:Title",
                Icons.Material.Filled.Terminal,
                "Categories:Infrastructure",
                addToNav: true,
                navOrder: 58));
    }
}

/// <summary>
/// Fluent guide for the local terminal UI module.
/// </summary>
public sealed class ModuleTerminalUIGuide : ModuleGuide<ModuleTerminalUI, ModuleTerminalUIOption, ModuleTerminalUIGuide>
{
}

/// <summary>
/// Options for the local terminal UI module.
/// </summary>
public sealed class ModuleTerminalUIOption : ModuleOptions<ModuleTerminalUI>
{
    /// <summary>
    /// Gets or sets whether the terminal page should be excluded from UI registration.
    /// </summary>
    public bool DisableTerminalPage { get; set; }
}
