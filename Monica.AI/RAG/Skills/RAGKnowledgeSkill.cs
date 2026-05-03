using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.KnowledgeBase.Services;
using Monica.AI.KnowledgeBase.Services.Support;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.AI.Services.Support;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.Skills.Models;
using Monica.Modules;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Skills;

/// <summary>
/// Provides semantic retrieval and document browsing over the selected RAG knowledge bases.
/// </summary>
public sealed class RAGKnowledgeSkill(
    KnowledgeToolService knowledgeToolService,
    KnowledgeBaseService knowledgeBaseService,
    IOptions<ModuleRAGOption> ragOptions,
    ILogger<RAGKnowledgeSkill> logger)
    : Skill<RAGKnowledgeSkill>
{
    private static readonly JsonSerializerOptions _toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ModuleRAGOption _ragOptions = ragOptions.Value;

    /// <inheritdoc />
    public override SkillDefinition Definition { get; } = new(
        "rag-knowledge",
        "Retrieve grounded facts and citations from indexed knowledge bases.",
        "Use these scripts when the user asks about content in the selected knowledge bases. " +
        "Rewrite the question into a focused retrieval query before semantic search. " +
        "For hybrid retrieval, first use knowledge-base-lookup search-knowledge-documents to find exact filenames, paths, " +
        "literal phrases, or regex matches, then run semantic search here for concept-level recall. " +
        "Use browse-knowledge-documents to discover candidate documents, browse-knowledge-document-tree to navigate folder hierarchy, " +
        "and get-knowledge-document-content to load source text. Always cite source name and source link.");

    /// <inheritdoc />
    public override IEnumerable<ModuleKey> RequiredModules => [BuiltInModuleKey.RAG];

    /// <summary>
    /// Gets the optional MCP server definition for external RAG clients.
    /// </summary>
    public override SkillMcpServerDefinition McpServerDefinition { get; } = new(
        "rag-knowledge",
        "Retrieve grounded semantic results and citations from selected RAG-enabled knowledge bases.")
    {
        Title = "RAG Knowledge",
        Instructions =
            "Run semantic retrieval over selected RAG-enabled knowledge bases. For hybrid retrieval, combine these results with knowledge-base-lookup fuzzy or regex document search.",
        EnabledByDefault = true
    };

    /// <summary>
    /// Semantic search over the selected knowledge bases.
    /// </summary>
    /// <param name="query">Focused retrieval query, not conversational text.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="userQuestion">Optional original user question for context.</param>
    /// <param name="topK">Optional top-K result count override.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized knowledge search payload.</returns>
    [SkillTool(
        Name = "search-knowledge-base",
        Description = "Semantic search over the selected knowledge bases. Returns ranked excerpts with source name and source link for citation.")]
    public async Task<string> SearchAsync(
        string query,
        IServiceProvider services,
        string? userQuestion = null,
        int? topK = null,
        CancellationToken ct = default)
    {
        var knowledgeBases = await LoadSelectedKnowledgeBasesAsync(services, ct);
        if (knowledgeBases.Count == 0)
        {
            return SerializeNoSelection("search-knowledge-base");
        }

        logger.LogInformation("Executing RAG knowledge search for query: {Query}.", query);
        var payload = await knowledgeToolService.SearchKnowledgeAsync(
            "search-knowledge-base",
            knowledgeBases,
            query,
            userQuestion,
            topK ?? _ragOptions.DefaultTopK,
            ct);

        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    /// <summary>
    /// Lists or fuzzy-searches indexed documents in the selected knowledge bases.
    /// </summary>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="query">Optional document title, path, or preview-context query.</param>
    /// <param name="maxDocuments">Optional maximum number of documents to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document browsing payload.</returns>
    [SkillTool(
        Name = "browse-knowledge-documents",
        Description = "List indexed knowledge-base documents or fuzzy-search document titles, paths, and preview contexts.")]
    public async Task<string> BrowseDocumentsAsync(
        IServiceProvider services,
        string? query = null,
        int? maxDocuments = null,
        CancellationToken ct = default)
    {
        var knowledgeBases = await LoadSelectedKnowledgeBasesAsync(services, ct);
        if (knowledgeBases.Count == 0)
        {
            return SerializeNoSelection("browse-knowledge-documents");
        }

        var payload = await knowledgeToolService.BrowseDocumentsAsync(
            "browse-knowledge-documents",
            knowledgeBases,
            query,
            maxDocuments,
            ct);

        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    /// <summary>
    /// Browses the indexed document tree for the selected knowledge bases.
    /// </summary>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="knowledgeBaseId">Optional knowledge-base id filter.</param>
    /// <param name="directoryPath">Optional directory path to root the tree at.</param>
    /// <param name="maxDepth">Optional maximum tree depth.</param>
    /// <param name="maxEntries">Optional maximum number of tree entries.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document tree payload.</returns>
    [SkillTool(
        Name = "browse-knowledge-document-tree",
        Description = "Browse the indexed document directory tree for selected knowledge bases.")]
    public async Task<string> BrowseDocumentTreeAsync(
        IServiceProvider services,
        string? knowledgeBaseId = null,
        string? directoryPath = null,
        int? maxDepth = null,
        int? maxEntries = null,
        CancellationToken ct = default)
    {
        var knowledgeBases = await LoadSelectedKnowledgeBasesAsync(services, ct);
        if (knowledgeBases.Count == 0)
        {
            return SerializeNoSelection("browse-knowledge-document-tree");
        }

        var payload = await knowledgeToolService.BrowseDocumentTreeAsync(
            "browse-knowledge-document-tree",
            knowledgeBases,
            knowledgeBaseId,
            directoryPath,
            maxDepth,
            maxEntries,
            ct);

        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    /// <summary>
    /// Loads original source content for one indexed knowledge-base document.
    /// </summary>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="documentId">Optional document id returned by search or browse scripts.</param>
    /// <param name="sourceLink">Optional source link returned by search or browse scripts.</param>
    /// <param name="knowledgeBaseId">Optional knowledge-base id to disambiguate document identity.</param>
    /// <param name="maxTokens">Optional maximum content token budget.</param>
    /// <param name="startCharacterIndex">Optional character offset used to continue a previous read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document content payload.</returns>
    [SkillTool(
        Name = "get-knowledge-document-content",
        Description = "Load original source content for a specific indexed knowledge-base document.")]
    public async Task<string> GetDocumentContentAsync(
        IServiceProvider services,
        string? documentId = null,
        string? sourceLink = null,
        string? knowledgeBaseId = null,
        int? maxTokens = null,
        int? startCharacterIndex = null,
        CancellationToken ct = default)
    {
        var knowledgeBases = await LoadSelectedKnowledgeBasesAsync(services, ct);
        if (knowledgeBases.Count == 0)
        {
            return SerializeNoSelection("get-knowledge-document-content");
        }

        var payload = await knowledgeToolService.GetDocumentContentAsync(
            "get-knowledge-document-content",
            knowledgeBases,
            documentId,
            sourceLink,
            knowledgeBaseId,
            maxTokens,
            startCharacterIndex,
            ct);

        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    private async Task<IReadOnlyList<KnowledgeBaseModel>> LoadSelectedKnowledgeBasesAsync(
        IServiceProvider services,
        CancellationToken ct)
    {
        var runtimeContext = services.GetRequiredService<IAIChatRuntimeContextAccessor>().Current;
        var selection = runtimeContext.GetOrDefault(KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection);
        if (selection?.KnowledgeBaseIds is not { Count: > 0 } selectedIds)
        {
            return [];
        }

        var knowledgeBases = new List<KnowledgeBaseModel>();
        foreach (var knowledgeBaseId in selectedIds
                     .Where(static id => !string.IsNullOrWhiteSpace(id))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var knowledgeBase = await knowledgeBaseService.GetByIdAsync(knowledgeBaseId, ct);
            if (knowledgeBase is not null && HasEmbeddingBinding(knowledgeBase))
            {
                knowledgeBases.Add(knowledgeBase);
            }
        }

        return knowledgeBases;
    }

    private static bool HasEmbeddingBinding(KnowledgeBaseModel knowledgeBase)
        => !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
           && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName);

    private static string SerializeNoSelection(string toolName)
    {
        return JsonSerializer.Serialize(
            new KnowledgeToolNoSelectionPayload
            {
                ToolName = toolName,
                Message = "No RAG-enabled knowledge base is selected for this chat session.",
                NextStepInstruction = "Use lookup scripts for non-RAG knowledge bases, or ask the user to configure an embedding model for semantic RAG retrieval.",
                ResultCount = 0
            },
            _toolJsonOptions);
    }
}
