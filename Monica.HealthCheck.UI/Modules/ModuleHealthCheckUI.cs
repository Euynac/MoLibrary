using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
                    accessPolicyType: typeof(HealthCheckPageAccess)));
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
    public override void ValidateOptions(ModuleHealthCheckUIOption options, string? profileName)
    {
        if (options.EnableOutsideDevelopment && string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            throw new InvalidOperationException(
                $"{nameof(ModuleHealthCheckUIOption.AuthorizationPolicy)} must name a non-empty host authorization " +
                $"policy when {nameof(ModuleHealthCheckUIOption.EnableOutsideDevelopment)} is enabled.");
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleHealthCheckUIOption> context)
    {
        context.Services.TryAddScoped<HealthCheckPageAccess>();
        context.Services.TryAddSingleton<HealthCheckPageSessionFactory>();
    }
}

/// <summary>
/// Configures access to the Health Check dashboard.
/// </summary>
public sealed class ModuleHealthCheckUIOption : ModuleOptions<ModuleHealthCheckUI>, IDevelopmentPageAccessOptions
{
    /// <summary>
    /// Gets or sets whether the dashboard may be exposed outside the Development environment. Defaults to
    /// <see langword="false"/>. Enabling it also requires a valid named <see cref="AuthorizationPolicy"/> from the host.
    /// </summary>
    public bool EnableOutsideDevelopment { get; set; }

    /// <summary>
    /// Gets or sets the host authorization policy evaluated for the current Blazor circuit outside Development.
    /// A missing, empty, or unregistered policy denies access.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }
}

/// <summary>
/// Applies the Health Check dashboard's Development-first, policy-gated access contract.
/// </summary>
public sealed class HealthCheckPageAccess(
    IHostEnvironment environment,
    IOptions<ModuleHealthCheckUIOption> options,
    IServiceProvider services)
    : DevelopmentPageAccessPolicy<ModuleHealthCheckUIOption>(environment, options, services);
