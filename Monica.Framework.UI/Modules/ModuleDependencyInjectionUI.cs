using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDependencyInjectionUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the dependency-injection diagnostics UI module.
        /// </summary>
        public static ModuleDependencyInjectionUIGuide AddDependencyInjectionUI(Action<ModuleDependencyInjectionUIOption>? action = null)
        {
            return new ModuleDependencyInjectionUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Dependency-injection diagnostics UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.DependencyInjectionUI)]
public class ModuleDependencyInjectionUI(ModuleDependencyInjectionUIOption option)
    : ModuleBase<ModuleDependencyInjectionUI, ModuleDependencyInjectionUIOption, ModuleDependencyInjectionUIGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // UI components inject the infrastructure facade directly.
    }

    public override void ClaimDependencies()
    {
        if (Option.DisablePage)
        {
            return;
        }

        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<DependencyInjectionResource>();

        DependsOnModule<ModuleDependencyInjectionGuide>().Register()
            .EnableAutoRegistrationDiagnostics();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIDependencyInjectionPage>(
                UIDependencyInjectionPage.PAGE_URL,
                "Pages:DependencyInjection:Title",
                Icons.Material.Filled.AccountTree,
                "Categories:Infrastructure",
                addToNav: true,
                navOrder: 10));
    }
}

/// <summary>
/// Fluent guide for the dependency-injection diagnostics UI module.
/// </summary>
public class ModuleDependencyInjectionUIGuide
    : ModuleGuide<ModuleDependencyInjectionUI, ModuleDependencyInjectionUIOption, ModuleDependencyInjectionUIGuide>
{
}

/// <summary>
/// Options for the dependency-injection diagnostics UI module.
/// </summary>
public class ModuleDependencyInjectionUIOption : ModuleOptions<ModuleDependencyInjectionUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the dependency-injection diagnostics page is disabled.
    /// </summary>
    public bool DisablePage { get; set; }
}
