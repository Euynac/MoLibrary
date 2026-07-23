using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDependencyInjectionUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the dependency-injection diagnostics UI module.
        /// </summary>
        public ModuleDependencyInjectionUIGuide AddDependencyInjectionUI(Action<ModuleDependencyInjectionUIOption>? action = null)
        {
            return builder.AddModule<ModuleDependencyInjectionUI, ModuleDependencyInjectionUIOption, ModuleDependencyInjectionUIGuide>(action);
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
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIDependencyInjectionPage, DependencyInjectionResource>(
                UIDependencyInjectionPage.PAGE_URL,
                "Pages:DependencyInjection:Title",
                Icons.Material.Filled.AccountTree,
                BuiltInNavigationCategoryIds.Infrastructure,
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
