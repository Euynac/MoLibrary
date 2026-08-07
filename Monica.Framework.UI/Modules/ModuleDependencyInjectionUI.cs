using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleDependencyInjectionUI, ModuleDependencyInjectionUIOption> AddDependencyInjectionUI(
            Action<ModuleDependencyInjectionUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleDependencyInjectionUI, ModuleDependencyInjectionUIOption>(action);
            registration.Require<ModuleDependencyInjection, ModuleDependencyInjectionOption>()
                .EnableAutoRegistrationDiagnostics();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<DependencyInjectionResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIDependencyInjectionPage, DependencyInjectionResource>(
                    UIDependencyInjectionPage.PAGE_URL,
                    "Pages:DependencyInjection:Title",
                    Icons.Material.Filled.AccountTree,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 10));
            return registration;
        }
    }
}

/// <summary>
/// Dependency-injection diagnostics UI module.
/// </summary>
public class ModuleDependencyInjectionUI : MonicaModule<ModuleDependencyInjectionUIOption>, IUIModule
{
    public override void ConfigureServices(ModuleContext<ModuleDependencyInjectionUIOption> context)
    {
        // UI components inject the infrastructure facade directly.
    }
}

/// <summary>
/// Options for the dependency-injection diagnostics UI module.
/// </summary>
public class ModuleDependencyInjectionUIOption : ModuleOptions<ModuleDependencyInjectionUI>
{
}
