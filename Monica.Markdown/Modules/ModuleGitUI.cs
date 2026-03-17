using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Markdown.Pages;
using Monica.Markdown.UIGit.Services;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Git dashboard UI module.
/// </summary>
public static class ModuleGitUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Git dashboard UI module.
        /// </summary>
        public static ModuleGitUIGuide AddGitUI(Action<ModuleGitUIOption>? action = null)
        {
            return new ModuleGitUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Git dashboard UI module.
/// </summary>
public class ModuleGitUI(ModuleGitUIOption option)
    : MoModule<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>(option)
{
    /// <inheritdoc />
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.GitUI;
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleGitGuide>().Register();

        if (!Option.DisableGitDashboardPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedComponent<UIGitRepositoriesPage>(
                        UIGitRepositoriesPage.PAGE_URL,
                        "Pages:GitRepositories:Title",
                        Icons.Material.Filled.Source,
                        "Categories:Documentation",
                        addToNav: true,
                        navOrder: 55);
                });
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<GitUIService>();
    }
}

/// <summary>
/// Fluent guide for the Git dashboard UI module.
/// </summary>
public class ModuleGitUIGuide : MoModuleGuide<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>
{
}

/// <summary>
/// Options for the Git dashboard UI module.
/// </summary>
public class ModuleGitUIOption : MoModuleOption<ModuleGitUI>
{
    /// <summary>
    /// Gets or sets whether the dashboard page should be disabled.
    /// </summary>
    public bool DisableGitDashboardPage { get; set; }
}
