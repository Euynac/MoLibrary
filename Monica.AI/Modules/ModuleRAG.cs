using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Monica.AI.Abstractions;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
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
    public RAGSearchOptions SearchOptions { get; set; } = new();
}

/// <summary>
/// RAG module configuration guide.
/// </summary>
public class ModuleRAGGuide
    : MoModuleGuide<ModuleRAG, ModuleRAGOption, ModuleRAGGuide>
{
    public ModuleRAGGuide UseKnowledgeBaseStore<TStore>()
        where TStore : class, IKnowledgeBaseStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IKnowledgeBaseStore, TStore>();
        });
        return this;
    }

    /// <summary>
    /// Uses the in-memory vector store (for development/testing).
    /// Vector dimensions are automatically derived from the embedding model in the catalog.
    /// </summary>
    public ModuleRAGGuide UseInMemoryVectorStore()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore>(sp =>
            {
                var providerFactory = sp.GetRequiredService<IAIProviderFactory>();
                var ragOptions = sp.GetRequiredService<IOptions<ModuleRAGOption>>().Value;

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
        });
        return this;
    }

    public ModuleRAGGuide UseVectorStore<TVectorStore>()
        where TVectorStore : VectorStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore, TVectorStore>();
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
