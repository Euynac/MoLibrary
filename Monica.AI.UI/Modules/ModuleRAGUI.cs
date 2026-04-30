using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.UIRAG.State;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
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
[ModuleKey(BuiltInModuleKey.RAGUI)]
public class ModuleRAGUI(ModuleRAGUIOption option)
    : ModuleBase<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RAGQueuePollingState>();
        services.AddScoped<RAGManagePageState>();
    }

    public override void ClaimDependencies()
    {
        // Depends on RAG backend module
        DependsOnModule<ModuleRAGGuide>().Register();
        DependsOnModule<ModuleKnowledgeBaseUIGuide>().Register();

        if (!Option.DisableRAGManagePage || !Option.DisableRAGDebugPage || !Option.DisableRAGChunkersPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<AIResource>();
        }

        // Depends on UI core module and register RAG pages
        if (!Option.DisableRAGManagePage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<RAGManagePage>(
                        RAGManagePage.PAGE_URL,
                        "Pages:RAGManage:Title",
                        Icons.Material.Filled.PlaylistPlay,
                        "Categories:KnowledgeRetrieval",
                        addToNav: true,
                        navOrder: 4);
                });
        }

        if (!Option.DisableRAGDebugPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<RAGDebugPage>(
                        RAGDebugPage.PAGE_URL,
                        "Pages:RAGDebug:Title",
                        Icons.Material.Filled.ManageSearch,
                        "Categories:KnowledgeRetrieval",
                        addToNav: true,
                        navOrder: 5);
                });
        }

        if (!Option.DisableRAGChunkersPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<RAGChunkersPage>(
                        RAGChunkersPage.PAGE_URL,
                        "Pages:RAGChunkers:Title",
                        Icons.Material.Filled.AccountTree,
                        "Categories:KnowledgeRetrieval",
                        addToNav: true,
                        navOrder: 6);
                });
        }
    }
}

/// <summary>
/// RAG UI module configuration guide.
/// </summary>
public class ModuleRAGUIGuide
    : ModuleGuide<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>
{
}

/// <summary>
/// RAG UI module configuration options.
/// </summary>
public class ModuleRAGUIOption : ModuleOptions<ModuleRAGUI>
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
