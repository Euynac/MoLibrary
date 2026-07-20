using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Facades;
using Monica.AI.KnowledgeBase.Providers;
using Monica.AI.KnowledgeBase.Services;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Markdown.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for configuring the knowledge-base module.
/// </summary>
public static class ModuleKnowledgeBaseBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures knowledge-base inventory and lookup services.
        /// </summary>
        /// <param name="action">Optional configuration action.</param>
        /// <returns>The knowledge-base module guide.</returns>
        public ModuleKnowledgeBaseGuide AddKnowledgeBase(Action<ModuleKnowledgeBaseOption>? action = null)
        {
            return builder.AddModule<ModuleKnowledgeBase, ModuleKnowledgeBaseOption, ModuleKnowledgeBaseGuide>(action);
        }
    }
}

/// <summary>
/// Knowledge-base inventory and lookup module.
/// </summary>
[ModuleKey(BuiltInModuleKey.KnowledgeBase)]
public sealed class ModuleKnowledgeBase(ModuleKnowledgeBaseOption option)
    : ModuleBase<ModuleKnowledgeBase, ModuleKnowledgeBaseOption, ModuleKnowledgeBaseGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleSkillSystemGuide>().Register();
        DependsOnModule<ModuleMarkdownGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IDocumentIndexStateStore, FileDocumentIndexStateStore>();
        services.TryAddSingleton<IKnowledgeDocumentSourceStore, FileKnowledgeDocumentSourceStore>();
        services.TryAddSingleton<IKnowledgeBaseStore, DocumentIndexStateKnowledgeBaseStore>();
        services.TryAddSingleton<KnowledgeBaseService>();
        services.TryAddSingleton<KnowledgeDocumentService>();
        services.TryAddSingleton<IKnowledgeDocumentQueryService, KnowledgeDocumentQueryService>();
        services.AddScoped(sp => new KnowledgeBaseFacade(
            sp.GetRequiredService<KnowledgeBaseService>(),
            sp.GetRequiredService<ILogger<KnowledgeBaseFacade>>()));
        services.AddScoped(sp => new KnowledgeDocumentFacade(
            sp.GetRequiredService<KnowledgeDocumentService>(),
            sp.GetRequiredService<IMarkdownDocumentCatalog>(),
            sp.GetRequiredService<ILogger<KnowledgeDocumentFacade>>()));
    }
}

/// <summary>
/// Knowledge-base module options.
/// </summary>
public sealed class ModuleKnowledgeBaseOption : ModuleOptions<ModuleKnowledgeBase>
{
    /// <summary>
    /// Relative file path for the unified knowledge-base and document index state store.
    /// Resolved relative to the application's running directory.
    /// </summary>
    public string DocumentIndexStateStoreFilePath { get; set; } = "monica_data/rag/document_index_state.json";

    /// <summary>
    /// Relative root path for source document content storage.
    /// </summary>
    public string UploadedDocumentSourceRootPath { get; set; } = "monica_data/rag/document_sources";
}

/// <summary>
/// Knowledge-base module guide.
/// </summary>
public sealed class ModuleKnowledgeBaseGuide
    : ModuleGuide<ModuleKnowledgeBase, ModuleKnowledgeBaseOption, ModuleKnowledgeBaseGuide>
{
    /// <summary>
    /// Uses a custom unified state store implementation.
    /// </summary>
    /// <typeparam name="TStore">Custom document index state store type.</typeparam>
    /// <returns>The current guide.</returns>
    public ModuleKnowledgeBaseGuide UseDocumentIndexStateStore<TStore>()
        where TStore : class, IDocumentIndexStateStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IDocumentIndexStateStore, TStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses the built-in file-based unified state store.
    /// </summary>
    /// <returns>The current guide.</returns>
    public ModuleKnowledgeBaseGuide UseDocumentIndexStateFileProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IDocumentIndexStateStore, FileDocumentIndexStateStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses a custom source content store implementation.
    /// </summary>
    /// <typeparam name="TStore">Custom source document store type.</typeparam>
    /// <returns>The current guide.</returns>
    public ModuleKnowledgeBaseGuide UseKnowledgeDocumentSourceStore<TStore>()
        where TStore : class, IKnowledgeDocumentSourceStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeDocumentSourceStore, TStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses the built-in file-based source content store.
    /// </summary>
    /// <returns>The current guide.</returns>
    public ModuleKnowledgeBaseGuide UseKnowledgeDocumentSourceFileProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeDocumentSourceStore, FileKnowledgeDocumentSourceStore>();
        });
        return this;
    }
}
