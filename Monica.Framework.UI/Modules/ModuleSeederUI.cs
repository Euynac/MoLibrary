using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using Monica.Framework.UI.UISeeder.State;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Provides host-bound registration for the read-only Seeder operations dashboard.
/// </summary>
public static class ModuleSeederUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the current-host Seeder dashboard, its localized navigation entry, and its access boundary.
        /// </summary>
        /// <param name="configure">Optional dashboard access configuration.</param>
        /// <returns>The host-bound Seeder UI module registration.</returns>
        public ModuleRegistration<ModuleSeederUI, ModuleSeederUIOption> AddSeederUI(
            Action<ModuleSeederUIOption>? configure = null)
        {
            var registration = builder.AddModule<ModuleSeederUI, ModuleSeederUIOption>(configure);
            registration.Require<ModuleSeeder, ModuleSeederOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<SeederResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UISeederPage, SeederResource>(
                    UISeederPage.PAGE_URL,
                    "Navigation:Title",
                    Icons.Material.Filled.PlaylistAddCheckCircle,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 15,
                    accessPolicyType: typeof(SeederPageAccess)));
            return registration;
        }
    }
}

/// <summary>
/// Registers presentation services for the current-host Seeder operations dashboard.
/// </summary>
public sealed class ModuleSeederUI : MonicaModule<ModuleSeederUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleSeeder, ModuleSeederOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
    }

    /// <inheritdoc />
    public override void ValidateOptions(ModuleSeederUIOption options, string? profileName)
    {
        if (options.EnableOutsideDevelopment && string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            throw new InvalidOperationException(
                $"{nameof(ModuleSeederUIOption.AuthorizationPolicy)} must name a non-empty host authorization " +
                $"policy when {nameof(ModuleSeederUIOption.EnableOutsideDevelopment)} is enabled.");
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleSeederUIOption> context)
    {
        context.Services.TryAddScoped<SeederPageAccess>();
        context.Services.TryAddSingleton<SeederPageSessionFactory>();
    }
}

/// <summary>
/// Configures access to the Seeder operations dashboard.
/// </summary>
public sealed class ModuleSeederUIOption : ModuleOptions<ModuleSeederUI>, IDevelopmentPageAccessOptions
{
    /// <summary>
    /// Gets or sets whether the dashboard may be exposed outside Development. The default is <see langword="false"/>.
    /// Enabling it also requires a valid named <see cref="AuthorizationPolicy"/> from the host.
    /// </summary>
    public bool EnableOutsideDevelopment { get; set; }

    /// <summary>
    /// Gets or sets the host authorization policy evaluated for the current Blazor circuit outside Development.
    /// Missing, empty, and unregistered policies deny access.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }
}

/// <summary>
/// Applies the Seeder dashboard's Development-first, policy-gated access contract.
/// </summary>
public sealed class SeederPageAccess(
    IHostEnvironment environment,
    IOptions<ModuleSeederUIOption> options,
    IServiceProvider services)
    : DevelopmentPageAccessPolicy<ModuleSeederUIOption>(environment, options, services);
