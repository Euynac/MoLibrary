using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Monica.AI.Models;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Services;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.AI.Modules;

/// <summary>
/// RAG module builder extensions.
/// </summary>
public static class ModuleRAGBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the RAG module.
        /// </summary>
        public static ModuleRAGGuide AddRAG(Action<ModuleRAGOption>? action = null)
        {
            return new ModuleRAGGuide().Register(action);
        }
    }
}

/// <summary>
/// RAG (Retrieval-Augmented Generation) module.
/// </summary>
public class ModuleRAG(ModuleRAGOption option)
    : MoModule<ModuleRAG, ModuleRAGOption, ModuleRAGGuide>(option)
{
    /// <inheritdoc />
    public override ModuleKey GetModuleKey() => EMoModuleKey.RAG;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RAGService>();
        services.AddSingleton<IDocumentChunker, MarkdownDocumentChunker>();
        services.AddSingleton<IDocumentQueueStore, Monica.AI.RAG.Stores.InMemoryDocumentQueueStore>();
    }
}

/// <summary>
/// RAG module options.
/// </summary>
public class ModuleRAGOption : MoModuleOption<ModuleRAG>
{
    public string CollectionNamePrefix { get; set; } = "monica_rag_";
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Relative file path for the file-based knowledge base store.
    /// Resolved relative to the application's running directory.
    /// </summary>
    public string KnowledgeBaseStoreFilePath { get; set; } = "monica_data/rag/knowledge_bases.json";

    /// <summary>
    /// Options for the TextSearchProvider used in agent integration (Phase 3).
    /// Controls search behavior (BeforeAIInvoke vs OnDemandFunctionCalling),
    /// result formatting, and recent message memory.
    /// </summary>
    public TextSearchProviderOptions? SearchProviderOptions { get; set; }
}

/// <summary>
/// RAG module configuration guide.
/// </summary>
public class ModuleRAGGuide
    : MoModuleGuide<ModuleRAG, ModuleRAGOption, ModuleRAGGuide>
{
    private const string CONFIG_KB_STORE = nameof(CONFIG_KB_STORE);
    private const string CONFIG_VECTOR_STORE = nameof(CONFIG_VECTOR_STORE);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_KB_STORE, CONFIG_VECTOR_STORE];
    }

    public ModuleRAGGuide UseKnowledgeBaseStore<TStore>()
        where TStore : class, IKnowledgeBaseStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeBaseStore, TStore>();
        }, key: CONFIG_KB_STORE);
        return this;
    }

    /// <summary>
    /// Uses the file-based knowledge base store.
    /// Persists knowledge base metadata as a JSON file on disk.
    /// File path is configured via <see cref="ModuleRAGOption.KnowledgeBaseStoreFilePath"/>.
    /// </summary>
    public ModuleRAGGuide UseKnowledgeBaseStoreFileProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeBaseStore, FileKnowledgeBaseStore>();
        }, key: CONFIG_KB_STORE);
        return this;
    }

    /// <summary>
    /// Uses the in-memory vector store (for development/testing).
    /// Embedding model selection is resolved at knowledge-base level in <see cref="RAGService"/>.
    /// </summary>
    public ModuleRAGGuide UseVectorStoreInMemoryProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore>(_ => new InMemoryVectorStore());
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    public ModuleRAGGuide UseVectorStoreProvider<TVectorStore>()
        where TVectorStore : VectorStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore, TVectorStore>();
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    /// <summary>
    /// Uses fake embeddings for testing and development through the unified AI provider pipeline.
    /// </summary>
    /// <param name="dimensions">Embedding dimensions.</param>
    /// <param name="providerId">Provider ID used for fake embedding provider.</param>
    /// <param name="modelName">Optional model name. If null, uses `fake-embeddings-{dimensions}d`.</param>
    public ModuleRAGGuide AddFakeEmbeddingsModel(
        int dimensions = 384,
        string providerId = "fake-embeddings",
        string? modelName = null)
    {
        var resolvedModelName = string.IsNullOrWhiteSpace(modelName)
            ? $"fake-embeddings-{dimensions}d"
            : modelName.Trim();

        DependsOnModule<ModuleAIGuide>().Register()
            .AddModel(new EmbeddingModelInfo
            {
                ModelName = resolvedModelName,
                Description = "Fake embedding model for development and testing.",
                Dimensions = dimensions
            })
            .AddFakeProvider(options =>
            {
                options.ProviderId = providerId;
                options.DisplayName ??= "Fake Embeddings";
                options.ApiKey = "fake";
                options.DefaultDimensions = dimensions;
                options.SupportedModels = [resolvedModelName];
            });

        return this;
    }

    public ModuleRAGGuide AddChunker<TChunker>()
        where TChunker : class, IDocumentChunker
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IDocumentChunker, TChunker>();
        });
        return this;
    }
}
