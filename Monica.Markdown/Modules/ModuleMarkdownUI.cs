using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Markdown.Pages;
using Monica.Markdown.UIMarkdown.Services;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.Markdown.Modules;

/// <summary>
/// Markdown UI module providing a document viewer with group selection,
/// tree navigation, and markdown rendering.
/// </summary>
public class ModuleMarkdownUI(ModuleMarkdownUIOption option)
    : MoModuleWithDependencies<ModuleMarkdownUI, ModuleMarkdownUIOption, ModuleMarkdownUIGuide>(option)
{
    /// <summary>
    /// Gets the module key for this UI module.
    /// </summary>
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.MarkdownUI;
    }

    /// <summary>
    /// Configures services for the Markdown UI module.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MarkdownUIService>();
    }

    /// <summary>
    /// Declares module dependencies.
    /// </summary>
    public override void ClaimDependencies()
    {
        if (!Option.DisableMarkdownPage)
        {
            DependsOnModule<ModuleMarkdownGuide>().Register();

            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIMarkdownPage>(
                    UIMarkdownPage.PAGE_URL,
                    "Markdown Documents",
                    Icons.Material.Filled.MenuBook,
                    "Documentation",
                    addToNav: true,
                    navOrder: 50));
        }
    }
}

public static class ModuleMarkdownUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Markdown UI module.
        /// </summary>
        public static ModuleMarkdownUIGuide AddMarkdownUI(Action<ModuleMarkdownUIOption>? action = null)
        {
            return new ModuleMarkdownUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Guide for the Markdown UI module.
/// </summary>
public class ModuleMarkdownUIGuide : MoModuleGuide<ModuleMarkdownUI, ModuleMarkdownUIOption, ModuleMarkdownUIGuide>
{
    /// <summary>
    /// Gets the requested configuration method keys.
    /// </summary>
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [];
    }
}

/// <summary>
/// Options for the Markdown UI module.
/// </summary>
public class ModuleMarkdownUIOption : MoModuleOptionWithMinimalApi<ModuleMarkdownUI>
{
    /// <summary>
    /// Whether to disable the Markdown documents page.
    /// </summary>
    public bool DisableMarkdownPage { get; set; } = false;
}
