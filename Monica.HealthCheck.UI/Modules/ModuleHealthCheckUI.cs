using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.HealthCheck.UI.Localization;
using Monica.HealthCheck.UI.Pages;
using Monica.HealthCheck.UI.State;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Provides registration helpers for Monica's unified Health Check UI module.
/// </summary>
public static class ModuleHealthCheckUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the current-host Health Check dashboard, its localized navigation entry, and its access boundary.
        /// </summary>
        /// <param name="action">Optional dashboard access configuration.</param>
        /// <returns>The host-bound Health Check UI module registration.</returns>
        public ModuleRegistration<ModuleHealthCheckUI, ModuleHealthCheckUIOption> AddHealthCheckUI(
            Action<ModuleHealthCheckUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleHealthCheckUI, ModuleHealthCheckUIOption>(action);
            registration.Require<ModuleHealthCheck, ModuleHealthCheckOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<HealthCheckResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<
                    UIHealthCheckPage,
                    HealthCheckResource>(
                    UIHealthCheckPage.PAGE_URL,
                    "Navigation:Title",
                    Icons.Material.Filled.MonitorHeart,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 10,
                    accessPolicyType: typeof(OperationalPageAccessPolicy<ModuleHealthCheckUIOption>)));
            return registration;
        }
    }
}

/// <summary>
/// Registers the presentation services for the current-host Health Check dashboard.
/// </summary>
public sealed class ModuleHealthCheckUI : MonicaModule<ModuleHealthCheckUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleHealthCheck, ModuleHealthCheckOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleHealthCheckUIOption> context)
    {
        context.Services.TryAddScoped<OperationalPageAccessPolicy<ModuleHealthCheckUIOption>>();
        context.Services.TryAddSingleton<HealthCheckPageSessionFactory>();
    }
}

/// <summary>
/// Configures access to the Health Check dashboard.
/// </summary>
public sealed class ModuleHealthCheckUIOption : ModuleOptions<ModuleHealthCheckUI>, IOperationalPageAccessOptions
{
    /// <summary>
    /// Gets or sets the host authorization policy that overrides the shell-wide operational policy for this page.
    /// The default is <see langword="null"/>. A missing or blank value inherits
    /// <see cref="OperationalPageAccessOption.AuthorizationPolicy"/>. Configure an override only when this page needs
    /// a different policy; Development access and operational Debug mode ignore it.
    /// </summary>
    public string? AuthorizationPolicyOverride { get; set; }
}
