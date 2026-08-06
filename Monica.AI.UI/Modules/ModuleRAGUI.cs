using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.UIRAG.State;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// RAG UI module builder extensions.
/// </summary>
public static class ModuleRAGUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the RAG UI module.
        /// </summary>
        public ModuleRegistration<ModuleRAGUI, ModuleRAGUIOption> AddRAGUI(
            Action<ModuleRAGUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleRAGUI, ModuleRAGUIOption>(action);
            registration.Require<ModuleKnowledgeBaseUI, ModuleKnowledgeBaseUIOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<AIResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedPage<RAGManagePage, AIResource>(
                        RAGManagePage.PAGE_URL,
                        "Pages:RAGManage:Title",
                        Icons.Material.Filled.PlaylistPlay,
                        BuiltInNavigationCategoryIds.KnowledgeRetrieval,
                        addToNav: true,
                        navOrder: 4);
                    registry.RegisterLocalizedPage<RAGDebugPage, AIResource>(
                        RAGDebugPage.PAGE_URL,
                        "Pages:RAGDebug:Title",
                        Icons.Material.Filled.ManageSearch,
                        BuiltInNavigationCategoryIds.KnowledgeRetrieval,
                        addToNav: true,
                        navOrder: 5);
                    registry.RegisterLocalizedPage<RAGChunkersPage, AIResource>(
                        RAGChunkersPage.PAGE_URL,
                        "Pages:RAGChunkers:Title",
                        Icons.Material.Filled.AccountTree,
                        BuiltInNavigationCategoryIds.KnowledgeRetrieval,
                        addToNav: true,
                        navOrder: 6);
                });
            return registration;
        }
    }
}

/// <summary>
/// RAG UI module.
/// Provides Blazor-based RAG debug and management interface.
/// </summary>
public class ModuleRAGUI : MonicaModule<ModuleRAGUIOption>, IUIModule
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleRAG, ModuleRAGOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleRAGUIOption> context)
    {
        context.Services.AddScoped<RAGQueuePollingState>();
        context.Services.AddScoped<RAGManagePageState>();
    }
}

/// <summary>
/// RAG UI module configuration options.
/// </summary>
public class ModuleRAGUIOption : ModuleOptions<ModuleRAGUI>
{
}
