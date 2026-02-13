using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Services;
using Monica.AI.Services;
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
    }
}

/// <summary>
/// RAG module options.
/// </summary>
public class ModuleRAGOption : MoModuleOption<ModuleRAG>
{
    public string? EmbeddingProviderId { get; set; }
    public string? EmbeddingModelName { get; set; }
    public int? VectorDimensions { get; set; }
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
    public ModuleRAGGuide UseFileKnowledgeBaseStore()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeBaseStore, FileKnowledgeBaseStore>();
        }, key: CONFIG_KB_STORE);
        return this;
    }

    /// <summary>
    /// Uses the in-memory vector store (for development/testing).
    /// Automatically resolves vector dimensions from the embedding model catalog
    /// and configures the embedding generator for auto-embedding.
    /// </summary>
    public ModuleRAGGuide UseInMemoryVectorStore()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore>(sp =>
            {
                var providerFactory = sp.GetRequiredService<IAIProviderFactory>();
                var modelCatalog = sp.GetRequiredService<AIModelCatalog>();
                var ragOptions = sp.GetRequiredService<IOptions<ModuleRAGOption>>().Value;

                // Auto-resolve VectorDimensions if not explicitly set
                ragOptions.VectorDimensions ??= ResolveVectorDimensions(
                    ragOptions, providerFactory, modelCatalog);

                var provider = !string.IsNullOrWhiteSpace(ragOptions.EmbeddingProviderId)
                    ? providerFactory.GetProvider(ragOptions.EmbeddingProviderId)
                      ?? throw new InvalidOperationException(
                          $"Embedding provider '{ragOptions.EmbeddingProviderId}' not found.")
                    : providerFactory.GetDefaultProvider()
                      ?? throw new InvalidOperationException(
                          "No default AI provider configured.");

                var embeddingGenerator = provider.GetEmbeddingGenerator(
                    ragOptions.EmbeddingModelName);

                return new InMemoryVectorStore(new()
                {
                    EmbeddingGenerator = embeddingGenerator
                });
            });
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    public ModuleRAGGuide UseVectorStore<TVectorStore>()
        where TVectorStore : VectorStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore, TVectorStore>();
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    /// <summary>
    /// Uses fake embeddings for testing and development.
    /// No external API or embedding model required.
    /// </summary>
    /// <remarks>
    /// This generates random embedding vectors, meaning semantic similarity search
    /// will return arbitrary results. Use this for:
    /// <list type="bullet">
    ///   <item>Unit and integration testing</item>
    ///   <item>CI/CD pipelines</item>
    ///   <item>Prototyping without API costs</item>
    ///   <item>Validating RAG infrastructure</item>
    /// </list>
    /// </remarks>
    /// <param name="dimensions">
    /// The number of dimensions for embedding vectors. Default is 384,
    /// which matches common small models like bge-micro-v2.
    /// </param>
    public ModuleRAGGuide UseFakeEmbeddings(int dimensions = 384)
    {
        ConfigureModuleOption(opt => opt.VectorDimensions = dimensions);

        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
                new FakeEmbeddingGenerator(dimensions));

            ctx.Services.AddSingleton<VectorStore>(sp =>
                new InMemoryVectorStore(new()
                {
                    EmbeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
                }));
        }, key: CONFIG_VECTOR_STORE);

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

    private static int ResolveVectorDimensions(
        ModuleRAGOption ragOptions,
        IAIProviderFactory providerFactory,
        AIModelCatalog modelCatalog)
    {
        if (ragOptions.VectorDimensions.HasValue)
            return ragOptions.VectorDimensions.Value;

        var provider = !string.IsNullOrWhiteSpace(ragOptions.EmbeddingProviderId)
            ? providerFactory.GetProvider(ragOptions.EmbeddingProviderId)
            : providerFactory.GetDefaultProvider();

        var modelName = ragOptions.EmbeddingModelName
            ?? provider?.Info.SupportedModels?
                .OfType<EmbeddingModelInfo>()
                .FirstOrDefault()?.ModelName;

        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException(
                "Cannot determine embedding model. Configure an embedding model " +
                "in the provider's SupportedModels or set VectorDimensions explicitly.");

        var modelInfo = modelCatalog.GetModel(modelName) as EmbeddingModelInfo
            ?? throw new InvalidOperationException(
                $"Embedding model '{modelName}' not found in catalog. " +
                "Register it via AddModel() or set VectorDimensions explicitly.");

        return modelInfo.Dimensions
            ?? throw new InvalidOperationException(
                $"Embedding model '{modelName}' has no Dimensions configured. " +
                "Use the probe feature in the provider management UI to detect dimensions, " +
                "or set VectorDimensions explicitly in RAG options.");
    }
}
