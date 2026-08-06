using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.AI.Services.Support;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.Skills.Models;
using Monica.Modules;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Skills;

/// <summary>
/// Provides semantic retrieval over selected RAG-enabled knowledge bases.
/// Literal document browse, search, tree, and content tools belong to KnowledgeBaseLookupSkill.
/// </summary>
internal sealed class RAGKnowledgeSkill(
    RAGSearchService searchService,
    IKnowledgeBaseStore knowledgeBaseStore,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGKnowledgeSkill> logger)
    : Skill<RAGKnowledgeSkill>
{
    private const int MAX_TOOL_RESULT_COUNT = 10;
    private static readonly JsonSerializerOptions TOOL_JSON_OPTIONS = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ModuleRAGOption _options = options.Value;

    /// <inheritdoc />
    public override SkillDefinition Definition { get; } = new(
        "rag-knowledge",
        "Retrieve grounded semantic results and citations from indexed knowledge bases.",
        "Rewrite the user question into a focused retrieval query before semantic search. " +
        "Use knowledge-base-lookup tools for literal phrases, filenames, folder navigation, and full source reads. " +
        "Always cite source name and source link from the returned results.");

    /// <inheritdoc />
    public override IReadOnlySet<Type> RequiredModules { get; } =
        new[] { typeof(ModuleRAG) }.ToFrozenSet();

    /// <inheritdoc />
    public override SkillMcpServerDefinition McpServerDefinition { get; } = new(
        "rag-knowledge",
        "Retrieve grounded semantic results and citations from selected RAG-enabled knowledge bases.")
    {
        Title = "RAG Knowledge",
        Instructions = "Run semantic retrieval and cite the returned sources. Use knowledge-base-lookup for literal source navigation.",
        EnabledByDefault = true
    };

    /// <summary>
    /// Performs semantic search over the RAG-enabled knowledge bases selected for this chat.
    /// </summary>
    [SkillTool(
        Name = "search-knowledge-base",
        Description = "Semantic search over selected RAG-enabled knowledge bases. Returns ranked excerpts and citation metadata.")]
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
            return JsonSerializer.Serialize(
                new KnowledgeToolNoSelectionPayload
                {
                    ToolName = "search-knowledge-base",
                    Message = "No RAG-enabled knowledge base is selected for this chat session.",
                    NextStepInstruction =
                        "Use knowledge-base-lookup for non-RAG sources, or configure an embedding model before semantic retrieval."
                },
                TOOL_JSON_OPTIONS);
        }

        var normalizedQuery = NormalizeQuery(query);
        if (normalizedQuery.Length == 0)
        {
            return JsonSerializer.Serialize(
                new KnowledgeSearchToolPayload
                {
                    ToolName = "search-knowledge-base",
                    SearchQuery = string.Empty,
                    UserQuestion = userQuestion,
                    Message = "A focused retrieval query is required.",
                    NextStepInstruction = "Rewrite the user request into a focused semantic query before retrying.",
                    CitationInstruction = "Cite SourceName and SourceLink for every grounded claim.",
                    CanRetryWithRefinedQuery = true,
                    KnowledgeBases = knowledgeBases.Select(static knowledgeBase => new KnowledgeSearchToolKnowledgeBase
                    {
                        Id = knowledgeBase.Id,
                        Name = knowledgeBase.Name,
                        Description = knowledgeBase.Description
                    }).ToList()
                },
                TOOL_JSON_OPTIONS);
        }

        var requestedTopK = topK.GetValueOrDefault();
        var effectiveTopK = Math.Clamp(
            requestedTopK > 0 ? requestedTopK : _options.DefaultTopK,
            1,
            MAX_TOOL_RESULT_COUNT);
        logger.LogInformation("Executing semantic knowledge search for query '{Query}'.", normalizedQuery);
        var results = await searchService.SearchAsync(
            normalizedQuery,
            knowledgeBases.Select(static knowledgeBase => knowledgeBase.Id),
            effectiveTopK,
            ct: ct);

        var knowledgeBaseNames = knowledgeBases.ToDictionary(
            static knowledgeBase => knowledgeBase.Id,
            static knowledgeBase => knowledgeBase.Name,
            StringComparer.OrdinalIgnoreCase);
        var payload = new KnowledgeSearchToolPayload
        {
            ToolName = "search-knowledge-base",
            SearchQuery = normalizedQuery,
            UserQuestion = userQuestion,
            Message = results.Count == 0
                ? "No semantically relevant indexed excerpts were found."
                : $"Found {results.Count} semantically relevant excerpt(s).",
            NextStepInstruction = results.Count == 0
                ? "Refine the semantic query or use knowledge-base-lookup for literal search."
                : "Answer from these excerpts and cite each source name and source link.",
            CitationInstruction = "Cite SourceName and SourceLink for every grounded claim.",
            ResultCount = results.Count,
            ShouldRetrySameQuery = false,
            CanRetryWithRefinedQuery = results.Count < Math.Min(2, effectiveTopK),
            KnowledgeBases = knowledgeBases.Select(static knowledgeBase => new KnowledgeSearchToolKnowledgeBase
            {
                Id = knowledgeBase.Id,
                Name = knowledgeBase.Name,
                Description = knowledgeBase.Description
            }).ToList(),
            Results = results.Select(result => new KnowledgeSearchToolResultItem
            {
                KnowledgeBaseId = result.KnowledgeBaseId,
                KnowledgeBaseName = result.KnowledgeBaseId is not null
                                    && knowledgeBaseNames.TryGetValue(result.KnowledgeBaseId, out var name)
                    ? name
                    : null,
                SourceName = result.SourceName,
                SourceLink = result.SourceLink,
                SectionPath = result.SectionPath,
                DocumentId = result.DocumentId,
                ChunkIndex = result.ChunkIndex,
                Score = result.Score,
                ScoreKind = result.ScoreKind.ToString(),
                SimilarityScore = result.SimilarityScore,
                Content = result.Text ?? string.Empty
            }).ToList()
        };

        return JsonSerializer.Serialize(payload, TOOL_JSON_OPTIONS);
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
        foreach (var knowledgeBaseId in selectedIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var knowledgeBase = await knowledgeBaseStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct);
            if (knowledgeBase is not null
                && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
                && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName))
            {
                knowledgeBases.Add(knowledgeBase);
            }
        }

        return knowledgeBases;
    }

    private static string NormalizeQuery(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : string.Join(
                ' ',
                query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
