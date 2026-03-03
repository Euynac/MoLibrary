using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddSingleton<ChunkerRegistry>();
        services.AddSingleton<IDocumentQueueStore, Monica.AI.RAG.Stores.InMemoryDocumentQueueStore>();
        services.TryAddSingleton<IChunkerRoutingStore, FileChunkerRoutingStore>();
        services.TryAddSingleton<IDocumentChunkSnapshotStore, FileDocumentChunkSnapshotStore>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDocumentChunker, ProductionMarkdownDocumentChunker>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDocumentChunker, SimpleMarkdownDocumentChunker>());
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
    /// Relative file path for extension-to-chunker routing configuration.
    /// </summary>
    public string ChunkerRoutingStoreFilePath { get; set; } = "monica_data/rag/chunker_routing.json";

    /// <summary>
    /// Relative file path for document chunk snapshots used by chunk viewer.
    /// </summary>
    public string DocumentChunkSnapshotStoreFilePath { get; set; } = "monica_data/rag/document_chunk_snapshots.json";

    /// <summary>
    /// Target chunk size for the production markdown chunker.
    /// </summary>
    public int ProductionChunkerTargetChars { get; set; } = 1200;

    /// <summary>
    /// Maximum chunk size for the production markdown chunker.
    /// </summary>
    public int ProductionChunkerMaxChars { get; set; } = 1800;

    /// <summary>
    /// Minimum chunk size for the production markdown chunker.
    /// </summary>
    public int ProductionChunkerMinChars { get; set; } = 200;

    /// <summary>
    /// Overlap size between adjacent chunks for the production markdown chunker.
    /// </summary>
    public int ProductionChunkerOverlapChars { get; set; } = 120;

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
    /// Uses a custom chunker routing store implementation.
    /// </summary>
    public ModuleRAGGuide UseChunkerRoutingStore<TStore>()
        where TStore : class, IChunkerRoutingStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IChunkerRoutingStore, TStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses file-based extension routing store.
    /// </summary>
    public ModuleRAGGuide UseChunkerRoutingFileProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IChunkerRoutingStore, FileChunkerRoutingStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses a custom document chunk snapshot store implementation.
    /// </summary>
    public ModuleRAGGuide UseDocumentChunkSnapshotStore<TStore>()
        where TStore : class, IDocumentChunkSnapshotStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IDocumentChunkSnapshotStore, TStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses file-based document chunk snapshot store.
    /// </summary>
    public ModuleRAGGuide UseDocumentChunkSnapshotFileProvider()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IDocumentChunkSnapshotStore, FileDocumentChunkSnapshotStore>();
        });
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
            ctx.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IDocumentChunker, TChunker>());
        });
        return this;
    }

    /// <summary>
    /// Registers one built-in chunker.
    /// </summary>
    public ModuleRAGGuide AddBuiltInChunker(RAGBuiltInChunkerType chunkerType)
    {
        return chunkerType switch
        {
            RAGBuiltInChunkerType.ProductionMarkdown => AddChunker<ProductionMarkdownDocumentChunker>(),
            RAGBuiltInChunkerType.SimpleMarkdown => AddChunker<SimpleMarkdownDocumentChunker>(),
            _ => this
        };
    }

    /// <summary>
    /// Registers selected built-in chunkers.
    /// </summary>
    public ModuleRAGGuide AddBuiltInChunkers(params RAGBuiltInChunkerType[] chunkerTypes)
    {
        if (chunkerTypes is null || chunkerTypes.Length == 0)
        {
            return AddBuiltInChunker(RAGBuiltInChunkerType.ProductionMarkdown)
                .AddBuiltInChunker(RAGBuiltInChunkerType.SimpleMarkdown);
        }

        foreach (var chunkerType in chunkerTypes.Distinct())
        {
            AddBuiltInChunker(chunkerType);
        }

        return this;
    }
}

/// <summary>
/// Built-in chunker kinds provided by ModuleRAG.
/// </summary>
public enum RAGBuiltInChunkerType
{
    ProductionMarkdown = 1,
    SimpleMarkdown = 2
}
