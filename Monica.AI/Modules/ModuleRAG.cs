using System.Net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Monica.AI.Abstractions;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Facades;
using Monica.AI.RAG.Models;
using Monica.AI.Services;
using Monica.AI.RAG.Services;
using Monica.AI.RAG.Services.Support;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

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
[ModuleKey(BuiltInModuleKey.RAG)]
public class ModuleRAG(ModuleRAGOption option)
    : ModuleBase<ModuleRAG, ModuleRAGOption, ModuleRAGGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register();
        DependsOnModule<ModuleMarkdownGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<ITokenCountProvider, EstimatedUtf8TokenCountProvider>();
        services.AddSingleton<RAGService>();
        services.AddSingleton<KnowledgeToolService>();
        services.AddSingleton<RAGEmbeddingBindingResolver>();
        services.AddSingleton<RAGVectorCollectionCoordinator>();
        services.AddSingleton<RAGIndexStateCoordinator>();
        services.AddSingleton<ChunkerRegistry>();
        services.TryAddSingleton<IChunkerRoutingStore, FileChunkerRoutingStore>();
        services.AddScoped<MarkdownDocumentResolver>();
        services.AddScoped<ChunkViewCoordinator>();
        services.AddScoped<BatchIndexCoordinator>();
        services.AddScoped<RAGFacade>();
        services.AddScoped<EmbeddingModelFacade>();
        services.AddScoped<ChunkerFacade>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDocumentChunker, ProductionMarkdownDocumentChunker>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDocumentChunker, SimpleMarkdownDocumentChunker>());
    }
}

/// <summary>
/// RAG module options.
/// </summary>
public class ModuleRAGOption : ModuleOptions<ModuleRAG>
{
    public string CollectionNamePrefix { get; set; } = "monica_rag_";
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Relative file path for extension-to-chunker routing configuration.
    /// </summary>
    public string ChunkerRoutingStoreFilePath { get; set; } = "monica_data/rag/chunker_routing.json";

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
/// Qdrant-specific settings for the RAG vector store provider.
/// </summary>
public class ModuleRAGQdrantOption : IModuleExtraOptions<ModuleRAG>
{
    /// <summary>
    /// Qdrant host name.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Qdrant gRPC port.
    /// </summary>
    public int Port { get; set; } = 6334;

    /// <summary>
    /// Whether to use HTTPS for the Qdrant connection.
    /// </summary>
    public bool Https { get; set; }

    /// <summary>
    /// Optional Qdrant API key.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// RAG module configuration guide.
/// </summary>
public class ModuleRAGGuide
    : ModuleGuide<ModuleRAG, ModuleRAGOption, ModuleRAGGuide>
{
    private const string CONFIG_INDEX_STATE_STORE = nameof(CONFIG_INDEX_STATE_STORE);
    private const string CONFIG_SOURCE_STORE = nameof(CONFIG_SOURCE_STORE);
    private const string CONFIG_VECTOR_STORE = nameof(CONFIG_VECTOR_STORE);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_VECTOR_STORE];
    }

    /// <summary>
    /// Uses a custom unified state store implementation.
    /// </summary>
    public ModuleRAGGuide UseDocumentIndexStateStore<TStore>()
        where TStore : class, IDocumentIndexStateStore
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register()
            .UseDocumentIndexStateStore<TStore>();
        ConfigureEmpty(CONFIG_INDEX_STATE_STORE);
        return this;
    }

    /// <summary>
    /// Uses the file-based unified state store.
    /// </summary>
    public ModuleRAGGuide UseDocumentIndexStateFileProvider()
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register()
            .UseDocumentIndexStateFileProvider();
        ConfigureEmpty(CONFIG_INDEX_STATE_STORE);
        return this;
    }

    /// <summary>
    /// Uses a custom source content store implementation.
    /// </summary>
    public ModuleRAGGuide UseKnowledgeDocumentSourceStore<TStore>()
        where TStore : class, IKnowledgeDocumentSourceStore
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register()
            .UseKnowledgeDocumentSourceStore<TStore>();
        ConfigureEmpty(CONFIG_SOURCE_STORE);
        return this;
    }

    /// <summary>
    /// Uses file-based source content store.
    /// </summary>
    public ModuleRAGGuide UseKnowledgeDocumentSourceFileProvider()
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register()
            .UseKnowledgeDocumentSourceFileProvider();
        ConfigureEmpty(CONFIG_SOURCE_STORE);
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
            ctx.Services.AddSingleton(new RAGVectorStoreRegistrationInfo
            {
                ProviderKind = "InMemory",
                ProviderDisplayName = "In-Memory"
            });
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    public ModuleRAGGuide UseVectorStoreProvider<TVectorStore>()
        where TVectorStore : VectorStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<VectorStore, TVectorStore>();
            ctx.Services.AddSingleton(new RAGVectorStoreRegistrationInfo
            {
                ProviderKind = "Custom",
                ProviderDisplayName = typeof(TVectorStore).Name
            });
        }, key: CONFIG_VECTOR_STORE);
        return this;
    }

    /// <summary>
    /// Uses Qdrant as the vector store provider.
    /// </summary>
    public ModuleRAGGuide UseVectorStoreQdrantProvider(
        Action<ModuleRAGQdrantOption>? action = null)
    {
        ConfigureExtraOption(action);
        ConfigureServices(ctx =>
        {
            var option = ctx.GetModuleExtraOption<ModuleRAGQdrantOption>();
            ctx.Services.AddQdrantVectorStore(
                NormalizeQdrantHost(option.Host, option.Https),
                option.Port,
                option.Https,
                option.ApiKey ?? string.Empty,
                new QdrantVectorStoreOptions());
            ctx.Services.AddSingleton(CreateQdrantVectorStoreRegistrationInfo(option));
        }, key: CONFIG_VECTOR_STORE);

        return this;
    }

    private static RAGVectorStoreRegistrationInfo CreateQdrantVectorStoreRegistrationInfo(ModuleRAGQdrantOption option)
    {
        return new RAGVectorStoreRegistrationInfo
        {
            ProviderKind = "Qdrant",
            ProviderDisplayName = "Qdrant",
            ConfigurationEntries =
            [
                new VectorStoreConfigurationEntry
                {
                    Key = nameof(ModuleRAGQdrantOption.Host),
                    Value = NormalizeQdrantHost(option.Host, option.Https)
                },
                new VectorStoreConfigurationEntry
                {
                    Key = nameof(ModuleRAGQdrantOption.Port),
                    Value = option.Port.ToString()
                },
                new VectorStoreConfigurationEntry
                {
                    Key = nameof(ModuleRAGQdrantOption.Https),
                    Value = option.Https.ToString()
                },
                new VectorStoreConfigurationEntry
                {
                    Key = nameof(ModuleRAGQdrantOption.ApiKey),
                    Value = string.IsNullOrWhiteSpace(option.ApiKey) ? "Not configured" : "Configured",
                    IsSensitive = true
                }
            ]
        };
    }

    private static string NormalizeQdrantHost(string host, bool https)
        // Qdrant is accessed through Qdrant.Client, which uses gRPC for the 6334 endpoint.
        // In this environment the app runs as a Windows .NET process while Qdrant is published
        // from Docker Compose on the host. Using "localhost" can make the gRPC client probe the
        // IPv6 loopback (::1) path first. We reproduced that ::1:6334 waits about 21 seconds and
        // then fails, while 127.0.0.1:6334 succeeds in a few milliseconds. That makes the first
        // CollectionExistsAsync on a fresh process appear extremely slow even though Qdrant itself
        // is healthy. Normalize "localhost" to the IPv4 loopback for non-HTTPS local connections
        // so the client skips the bad ::1 path and reaches the published gRPC port immediately.
        => !https && string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Loopback.ToString()
            : host;

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
    /// Uses fake embeddings for testing and development through the unified AI provider pipeline.
    /// </summary>
    /// <param name="dimensions">Embedding dimensions.</param>
    /// <param name="providerId">Optional provider ID used for fake embedding provider. Defaults to provider type.</param>
    /// <param name="modelName">Optional model name. If null, uses `Fake-Embeddings-{dimensions}d`.</param>
    public ModuleRAGGuide AddFakeEmbeddingsModel(
        int dimensions = 384,
        string? providerId = null,
        string? modelName = null)
    {
        var resolvedProviderId = providerId ?? nameof(EAIProviderType.Fake);
        var resolvedModelName = string.IsNullOrWhiteSpace(modelName)
            ? $"Fake-Embeddings-{dimensions}d"
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
                options.ApiKey = "fake";
                options.DefaultDimensions = dimensions;
                options.SupportedModels = [resolvedModelName];
            }, resolvedProviderId);

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
