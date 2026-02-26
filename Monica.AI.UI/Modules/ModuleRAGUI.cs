using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Modules;
using Monica.AI.UI.Pages;
using Monica.AI.UI.Services;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.AI.UI.Modules;

/// <summary>
/// RAG UI module builder extensions.
/// </summary>
public static class ModuleRAGUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the RAG UI module.
        /// </summary>
        public static ModuleRAGUIGuide AddRAGUI(Action<ModuleRAGUIOption>? action = null)
        {
            return new ModuleRAGUIGuide().Register(action);
        }
    }
}

/// <summary>
/// RAG UI module.
/// Provides Blazor-based RAG debug and management interface.
/// </summary>
public class ModuleRAGUI(ModuleRAGUIOption option)
    : MoModuleWithDependencies<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.RAGUI;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RAGUIService>();
    }

    public override void ClaimDependencies()
    {
        // Depends on RAG backend module
        DependsOnModule<ModuleRAGGuide>().Register();

        // Depends on UI core module and register RAG pages
        if (!Option.DisableRAGManagePage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<UIAIRAGManagePage>(
                        UIAIRAGManagePage.PAGE_URL,
                        "Pages:RAGManage:Title",
                        Icons.Material.Filled.Storage,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 3);
                });
        }

        if (!Option.DisableRAGDebugPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<UIAIRAGDebugPage>(
                        UIAIRAGDebugPage.PAGE_URL,
                        "Pages:RAGDebug:Title",
                        Icons.Material.Filled.ManageSearch,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 4);
                });
        }
    }
}

/// <summary>
/// RAG UI module configuration guide.
/// </summary>
public class ModuleRAGUIGuide
    : MoModuleGuide<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>
{
}

/// <summary>
/// RAG UI module configuration options.
/// </summary>
public class ModuleRAGUIOption : MoModuleOption<ModuleRAGUI>
{
    /// <summary>
    /// Disable the RAG management page.
    /// </summary>
    public bool DisableRAGManagePage { get; set; }

    /// <summary>
    /// Disable the RAG debug page.
    /// </summary>
    public bool DisableRAGDebugPage { get; set; }
}
