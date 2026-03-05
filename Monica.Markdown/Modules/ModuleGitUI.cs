using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Markdown.Pages;
using Monica.Markdown.UIGit.Services;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.Markdown.Modules;

/// <summary>
/// Git UI module providing repository and credential management pages.
/// </summary>
public class ModuleGitUI(ModuleGitUIOption option)
    : MoModuleWithDependencies<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>(option)
{
    /// <inheritdoc />
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.GitUI;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<GitUIService>();
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleGitGuide>().Register();

        DependsOnModule<ModuleUICoreGuide>().Register()
            .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIGitPage>(
                UIGitPage.PAGE_URL,
                "Pages:GitRepositories:Title",
                Icons.Material.Filled.Source,
                "Categories:Documentation",
                addToNav: true,
                navOrder: 55));
    }
}

public static class ModuleGitUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Git UI module.
        /// </summary>
        public static ModuleGitUIGuide AddGitUI(Action<ModuleGitUIOption>? action = null)
        {
            return new ModuleGitUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Guide for the Git UI module.
/// </summary>
public class ModuleGitUIGuide : MoModuleGuide<ModuleGitUI, ModuleGitUIOption, ModuleGitUIGuide>
{
    /// <inheritdoc />
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [];
    }
}

/// <summary>
/// Options for the Git UI module.
/// </summary>
public class ModuleGitUIOption : MoModuleOption<ModuleGitUI>;
