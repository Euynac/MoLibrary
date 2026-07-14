using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Repository.UI.Localization;
using Monica.Repository.UI.Pages;
using Monica.Repository.UI.UIRepository.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Repository UI module.
/// </summary>
public static class ModuleRepositoryUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the Repository diagnostics UI module.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The Repository UI guide used for chained configuration.</returns>
        public ModuleRepositoryUIGuide AddRepositoryUI(Action<ModuleRepositoryUIOption>? action = null)
        {
            return builder.AddModule<ModuleRepositoryUI, ModuleRepositoryUIOption, ModuleRepositoryUIGuide>(action);
        }
    }
}

/// <summary>
/// Repository diagnostics UI module.
/// </summary>
/// <param name="option">The module options.</param>
[ModuleKey(BuiltInModuleKey.RepositoryUI)]
public sealed class ModuleRepositoryUI(ModuleRepositoryUIOption option)
    : ModuleBase<ModuleRepositoryUI, ModuleRepositoryUIOption, ModuleRepositoryUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<RepositoryUIResource>();

        DependsOnModule<ModuleRepositoryGuide>().Register();

        if (!Option.DisableRepositoryPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedComponent<UIRepositoryDashboardPage>(
                        UIRepositoryDashboardPage.PAGE_URL,
                        "Pages:RepositoryDashboard:Title",
                        Icons.Material.Filled.Storage,
                        "Categories:Monitor",
                        addToNav: true,
                        navOrder: 35);
                });
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RepositoryDashboardState>();
    }
}

/// <summary>
/// Fluent guide for the Repository UI module.
/// </summary>
public sealed class ModuleRepositoryUIGuide
    : ModuleGuide<ModuleRepositoryUI, ModuleRepositoryUIOption, ModuleRepositoryUIGuide>
{
}

/// <summary>
/// Configuration options for the Repository diagnostics UI module.
/// </summary>
public sealed class ModuleRepositoryUIOption : ModuleOptions<ModuleRepositoryUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the Repository diagnostics dashboard is disabled.
    /// </summary>
    public bool DisableRepositoryPage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether migration update actions are allowed while the host runs in Production.
    /// The default is <c>false</c>, so Production dashboards remain read-only unless the host explicitly opts in.
    /// </summary>
    public bool AllowMigrationUpdateInProduction { get; set; }

    /// <summary>
    /// Gets or sets the command timeout used while the dashboard applies EF Core migrations.
    /// The default is one hour, matching the legacy migration endpoint behavior.
    /// </summary>
    public TimeSpan MigrationCommandTimeout { get; set; } = TimeSpan.FromMinutes(60);
}
