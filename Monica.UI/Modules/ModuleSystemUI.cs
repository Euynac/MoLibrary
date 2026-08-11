using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
using Monica.UI.UIModuleSystem.State;
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
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<
                    ModuleSystemPage,
                    ModuleSystemResource>(
                    ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL,
                    "Navigation:Title",
                    Icons.Material.Filled.AccountTree,
                    BuiltInNavigationCategoryIds.Module,
                    addToNav: true,
                    navOrder: 10,
                    accessPolicyType: typeof(OperationalPageAccessPolicy<ModuleSystemUIOption>)));
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
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleSystem, ModuleSystemOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleSystemUIOption> context)
    {
        context.Services.AddSingleton<ModuleSystemWorkbenchSessionFactory>();
        context.Services.TryAddScoped<OperationalPageAccessPolicy<ModuleSystemUIOption>>();
    }
}

/// <summary>
/// Options for the module system dashboard UI module.
/// </summary>
public class ModuleSystemUIOption : ModuleOptions<ModuleSystemUI>, IOperationalPageAccessOptions
{
    /// <summary>
    /// Gets or sets the host authorization policy that overrides the shell-wide operational policy for this page.
    /// The default is <see langword="null"/>. A missing or blank value inherits
    /// <see cref="OperationalPageAccessOption.AuthorizationPolicy"/>. Configure an override only when this page needs
    /// a different policy; Development access and operational Debug mode ignore it.
    /// </summary>
    public string? AuthorizationPolicyOverride { get; set; }
}
