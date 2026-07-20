using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DevOps.Git.Pages;
using Monica.DevOps.Localization;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Git dashboard UI module.
/// </summary>
public static class ModuleGitUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the Git dashboard UI module.
        /// </summary>
        public ModuleGitUIGuide AddGitUI(Action<ModuleGitUIOption>? action = null)
        {
            return builder.AddModule<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>(action);
        }
    }
}

/// <summary>
/// Git dashboard UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.GitUI)]
public class ModuleGitUI(ModuleGitUIOption option)
    : ModuleBase<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleGitGuide>().Register();

        if (!Option.DisableGitDashboardPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<GitResource>();

            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedComponent<UIGitRepositoriesPage>(
                        UIGitRepositoriesPage.PAGE_URL,
                        "Pages:GitRepositories:Title",
                        Icons.Material.Filled.Source,
                        "Categories:Infrastructure",
                        addToNav: true,
                        navOrder: 55);
                });
        }
    }
}

/// <summary>
/// Fluent guide for the Git dashboard UI module.
/// </summary>
public class ModuleGitUIGuide : ModuleGuide<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>
{
}

/// <summary>
/// Options for the Git dashboard UI module.
/// </summary>
public class ModuleGitUIOption : ModuleOptions<ModuleGitUI>
{
    /// <summary>
    /// Gets or sets whether the dashboard page should be disabled.
    /// </summary>
    public bool DisableGitDashboardPage { get; set; }
}
