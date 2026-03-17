using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Pages;
using Monica.AI.UI.Services;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

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
    : MoModule<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.RAGUI;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RAGMarkdownDocumentResolver>();
        services.AddScoped<RAGChunkViewCoordinator>();
        services.AddScoped<RAGBatchIndexCoordinator>();
        services.AddScoped<RAGUIService>();
        services.AddScoped<IEmbeddingModelManagementUIService, EmbeddingModelManagementUIService>();
        services.AddScoped<IChunkerManagementUIService, ChunkerManagementUIService>();
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

        if (!Option.DisableRAGChunkersPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<UIAIRAGChunkersPage>(
                        UIAIRAGChunkersPage.PAGE_URL,
                        "Pages:RAGChunkers:Title",
                        Icons.Material.Filled.AccountTree,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 5);
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

    /// <summary>
    /// Disable the RAG chunkers page.
    /// </summary>
    public bool DisableRAGChunkersPage { get; set; }
}
