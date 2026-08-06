using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Models;
using Monica.UI.UIModuleSystem.State;
using Monica.UI.UIModuleSystem.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the module system dashboard UI module.
        /// </summary>
        public ModuleRegistration<ModuleSystemUI, ModuleSystemUIOption> AddModuleSystemUI(
            Action<ModuleSystemUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleSystemUI, ModuleSystemUIOption>(action);
            registration.Require<ModuleSystem, ModuleSystemOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<ModuleSystemResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<ModuleSystemPage, ModuleSystemResource>(
                    ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL,
                    "Navigation:Title",
                    Icons.Material.Filled.AccountTree,
                    BuiltInNavigationCategoryIds.Module,
                    addToNav: true,
                    navOrder: 10));
            return registration;
        }
    }
}

/// <summary>
/// Module system dashboard UI module.
/// </summary>
public class ModuleSystemUI : MonicaModule<ModuleSystemUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void ValidateOptions(ModuleSystemUIOption options, string? profileName)
    {
        if (options.EnableOutsideDevelopment && string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            throw new InvalidOperationException(
                $"{nameof(ModuleSystemUIOption.AuthorizationPolicy)} must name a non-empty host authorization policy " +
                $"when {nameof(ModuleSystemUIOption.EnableOutsideDevelopment)} is enabled.");
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleSystemUIOption> context)
    {
        context.Services.AddSingleton<ModuleSystemWorkbenchSessionFactory>();
        context.Services.AddScoped<ModuleSystemWorkbenchAccess>();
    }
}

/// <summary>
/// Options for the module system dashboard UI module.
/// </summary>
public class ModuleSystemUIOption : ModuleOptions<ModuleSystemUI>
{
    /// <summary>
    /// Gets or sets whether the read-only workbench may be exposed outside the Development environment.
    /// Defaults to <see langword="false"/>. Enabling it also requires a non-empty
    /// <see cref="AuthorizationPolicy"/> registered by the host.
    /// </summary>
    public bool EnableOutsideDevelopment { get; set; }

    /// <summary>
    /// Gets or sets the host authorization policy required for every non-Development workbench circuit.
    /// The policy is evaluated before Core diagnostics are invoked and before the shell shows navigation.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }
}
