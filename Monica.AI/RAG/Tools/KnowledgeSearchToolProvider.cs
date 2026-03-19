using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.AI.Tools;
using Monica.Modules;

namespace Monica.AI.RAG.Tools;

/// <summary>
/// Adds knowledge-base retrieval support to chat agents by wiring semantic search and document browsing tools.
/// </summary>
public class KnowledgeSearchToolProvider(
    KnowledgeToolService knowledgeToolService,
    RAGService ragService,
    IOptions<ModuleRAGOption> ragOptions,
    ILoggerFactory loggerFactory)
    : IAIChatToolProvider
{
    private const string DefaultSearchToolName = "search_knowledge_base";
    private const string DefaultBrowseDocumentsToolName = "browse_knowledge_documents";
    private const string DefaultBrowseTreeToolName = "browse_knowledge_document_tree";
    private const string DefaultDocumentContentToolName = "get_knowledge_document_content";
    private const string CitationInstruction =
        "Prefer citing the source name and source link when using these knowledge results.";

    private static readonly JsonSerializerOptions s_toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ModuleRAGOption _ragOptions = ragOptions.Value;
    private readonly ILogger _logger = loggerFactory.CreateLogger<KnowledgeSearchToolProvider>();

    /// <inheritdoc />
    public async Task ConfigureAsync(
        AIChatAgentBuilder builder,
        AIChatAgentCreateContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        var knowledgeBaseIds = context.KnowledgeBaseIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (knowledgeBaseIds.Count == 0)
        {
            return;
        }

        var knowledgeBases = await LoadKnowledgeBasesAsync(knowledgeBaseIds, ct);
        var searchProviderOptions = BuildSearchProviderOptions(
            _ragOptions.SearchProviderOptions,
            knowledgeBases);
        var searchAdapter = ragService.CreateSearchAdapter(knowledgeBaseIds, _ragOptions.DefaultTopK);

        _logger.LogInformation(
            "Configuring knowledge search support for knowledge bases [{KnowledgeBaseIds}] with search behavior {SearchBehavior}.",
            string.Join(", ", knowledgeBaseIds),
            searchProviderOptions.SearchTime);

        if (searchProviderOptions.SearchTime == TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling)
        {
            var searchToolName = searchProviderOptions.FunctionToolName ?? DefaultSearchToolName;
            var browseToolName = DefaultBrowseDocumentsToolName;
            var browseTreeToolName = DefaultBrowseTreeToolName;
            var documentContentToolName = DefaultDocumentContentToolName;

            _logger.LogInformation(
                "Registering knowledge tools '{SearchToolName}', '{BrowseToolName}', '{TreeToolName}', and '{ContentToolName}'.",
                searchToolName,
                browseToolName,
                browseTreeToolName,
                documentContentToolName);

            builder.AddTool(CreateKnowledgeSearchTool(searchToolName, searchProviderOptions, knowledgeBases));
            builder.AddTool(CreateBrowseDocumentsTool(browseToolName, knowledgeBases));
            builder.AddTool(CreateBrowseTreeTool(browseTreeToolName, knowledgeBases));
            builder.AddTool(CreateDocumentContentTool(documentContentToolName, knowledgeBases));
            builder.EnableAutomaticToolCalling();
            builder.EnableMultipleToolCalling();
            builder.AppendInstructions(BuildKnowledgeToolInstruction(
                searchToolName,
                browseToolName,
                browseTreeToolName,
                documentContentToolName));
            return;
        }

        var textSearchProvider = new TextSearchProvider(searchAdapter, options: searchProviderOptions, loggerFactory);
        builder.AddContextProvider(textSearchProvider);
    }

    private async Task<IReadOnlyList<KnowledgeBase>> LoadKnowledgeBasesAsync(
        IEnumerable<string> knowledgeBaseIds,
        CancellationToken ct)
    {
        var knowledgeBases = new List<KnowledgeBase>();
        foreach (var knowledgeBaseId in knowledgeBaseIds)
        {
            var knowledgeBase = await ragService.GetKnowledgeBaseByIdAsync(knowledgeBaseId, ct);
            if (knowledgeBase is not null)
            {
                knowledgeBases.Add(knowledgeBase);
            }
        }

        return knowledgeBases;
    }

    private TextSearchProviderOptions BuildSearchProviderOptions(
        TextSearchProviderOptions? configuredOptions,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        var searchTime = configuredOptions?.SearchTime
            ?? TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling;
        var toolName = string.IsNullOrWhiteSpace(configuredOptions?.FunctionToolName)
            ? DefaultSearchToolName
            : configuredOptions.FunctionToolName;
        var toolDescription = string.IsNullOrWhiteSpace(configuredOptions?.FunctionToolDescription)
            ? BuildKnowledgeSearchToolDescription(knowledgeBases)
            : configuredOptions.FunctionToolDescription;

        return new TextSearchProviderOptions
        {
            SearchTime = searchTime,
            FunctionToolName = toolName,
            FunctionToolDescription = toolDescription,
            ContextPrompt = configuredOptions?.ContextPrompt,
            CitationsPrompt = configuredOptions?.CitationsPrompt,
            ContextFormatter = configuredOptions?.ContextFormatter
                ?? (searchTime == TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling
                    ? new Func<IList<TextSearchProvider.TextSearchResult>, string>(results => FormatKnowledgeToolPayload(toolName, results, knowledgeBases))
                    : null),
            RecentMessageMemoryLimit = configuredOptions?.RecentMessageMemoryLimit ?? 0,
            StateKey = configuredOptions?.StateKey,
            SearchInputMessageFilter = configuredOptions?.SearchInputMessageFilter,
            StorageInputRequestMessageFilter = configuredOptions?.StorageInputRequestMessageFilter,
            StorageInputResponseMessageFilter = configuredOptions?.StorageInputResponseMessageFilter,
            RecentMessageRolesIncluded = configuredOptions?.RecentMessageRolesIncluded is { Count: > 0 } roles
                ? [.. roles]
                : null
        };
    }

    private static string BuildKnowledgeSearchToolDescription(IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        const string defaultDescription =
            "Search the selected knowledge bases for grounded facts, source excerpts, and citation-ready references that help answer the user question. Rewrite the user's request into a focused retrieval query before calling.";

        return AppendKnowledgeBaseCatalog(defaultDescription, knowledgeBases);
    }

    private static string BuildBrowseDocumentsToolDescription(IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        const string defaultDescription =
            "List indexed knowledge-base documents when query is empty, or fuzzy-search document titles, paths, and preview contexts when query is provided. Use this to discover candidate DocumentIds and SourceLinks.";

        return AppendKnowledgeBaseCatalog(defaultDescription, knowledgeBases);
    }

    private static string BuildDocumentContentToolDescription(IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        const string defaultDescription =
            "Load the original content for a specific knowledge-base document by DocumentId or SourceLink. Use startCharacterIndex to continue reading from a later position, and increase maxTokens when you need a larger excerpt.";

        return AppendKnowledgeBaseCatalog(defaultDescription, knowledgeBases);
    }

    private static string BuildDocumentTreeToolDescription(IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        const string defaultDescription =
            "Browse the indexed document directory tree by directory path. Use this when you know folder names, need the overall document hierarchy, or want to navigate toward a specific file path before loading its original content.";

        return AppendKnowledgeBaseCatalog(defaultDescription, knowledgeBases);
    }

    private static string AppendKnowledgeBaseCatalog(
        string description,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        if (knowledgeBases.Count == 0)
        {
            return description;
        }

        var descriptionBuilder = new StringBuilder(description);
        descriptionBuilder.Append(" Available knowledge bases: ");

        var knowledgeBaseDescriptions = knowledgeBases
            .Select(knowledgeBase =>
            {
                var descriptor = string.IsNullOrWhiteSpace(knowledgeBase.SearchToolDescription)
                    ? knowledgeBase.Description
                    : knowledgeBase.SearchToolDescription;

                return string.IsNullOrWhiteSpace(descriptor)
                    ? knowledgeBase.Name
                    : $"{knowledgeBase.Name} ({descriptor})";
            });

        descriptionBuilder.Append(string.Join("; ", knowledgeBaseDescriptions));
        return descriptionBuilder.ToString();
    }

    private AITool CreateKnowledgeSearchTool(
        string toolName,
        TextSearchProviderOptions options,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        async Task<string> SearchKnowledgeAsync(
            string searchQuery,
            string? userQuestion = null,
            int? topK = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Executing knowledge search tool '{ToolName}' for retrieval query: {Query}.",
                toolName,
                searchQuery);

            var payload = await knowledgeToolService.SearchKnowledgeAsync(
                toolName,
                knowledgeBases,
                searchQuery,
                userQuestion,
                topK,
                cancellationToken);

            return JsonSerializer.Serialize(payload, s_toolJsonOptions);
        }

        return AIFunctionFactory.Create(
            SearchKnowledgeAsync,
            name: toolName,
            description: options.FunctionToolDescription ?? BuildKnowledgeSearchToolDescription(knowledgeBases));
    }

    private AITool CreateBrowseDocumentsTool(
        string toolName,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        async Task<string> BrowseKnowledgeDocumentsAsync(
            string? query = null,
            int? maxDocuments = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Executing knowledge document browsing tool '{ToolName}' for query: {Query}.",
                toolName,
                query);

            var payload = await knowledgeToolService.BrowseDocumentsAsync(
                toolName,
                knowledgeBases,
                query,
                maxDocuments,
                cancellationToken);

            return JsonSerializer.Serialize(payload, s_toolJsonOptions);
        }

        return AIFunctionFactory.Create(
            BrowseKnowledgeDocumentsAsync,
            name: toolName,
            description: BuildBrowseDocumentsToolDescription(knowledgeBases));
    }

    private AITool CreateDocumentContentTool(
        string toolName,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        async Task<string> GetKnowledgeDocumentContentAsync(
            string? documentId = null,
            string? sourceLink = null,
            string? knowledgeBaseId = null,
            int? maxTokens = null,
            int? startCharacterIndex = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Executing knowledge document content tool '{ToolName}' for document '{DocumentId}' / source link '{SourceLink}'.",
                toolName,
                documentId,
                sourceLink);

            var payload = await knowledgeToolService.GetDocumentContentAsync(
                toolName,
                knowledgeBases,
                documentId,
                sourceLink,
                knowledgeBaseId,
                maxTokens,
                startCharacterIndex,
                cancellationToken);

            return JsonSerializer.Serialize(payload, s_toolJsonOptions);
        }

        return AIFunctionFactory.Create(
            GetKnowledgeDocumentContentAsync,
            name: toolName,
            description: BuildDocumentContentToolDescription(knowledgeBases));
    }

    private AITool CreateBrowseTreeTool(
        string toolName,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        async Task<string> BrowseKnowledgeDocumentTreeAsync(
            string? knowledgeBaseId = null,
            string? directoryPath = null,
            int? maxDepth = null,
            int? maxEntries = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Executing knowledge document tree tool '{ToolName}' for knowledge base '{KnowledgeBaseId}' and directory '{DirectoryPath}'.",
                toolName,
                knowledgeBaseId,
                directoryPath);

            var payload = await knowledgeToolService.BrowseDocumentTreeAsync(
                toolName,
                knowledgeBases,
                knowledgeBaseId,
                directoryPath,
                maxDepth,
                maxEntries,
                cancellationToken);

            return JsonSerializer.Serialize(payload, s_toolJsonOptions);
        }

        return AIFunctionFactory.Create(
            BrowseKnowledgeDocumentTreeAsync,
            name: toolName,
            description: BuildDocumentTreeToolDescription(knowledgeBases));
    }

    private static string BuildKnowledgeToolInstruction(
        string searchToolName,
        string browseToolName,
        string browseTreeToolName,
        string documentContentToolName)
    {
        return
            $"When the user asks about content in the selected knowledge bases, you may use `{searchToolName}`, `{browseToolName}`, `{browseTreeToolName}`, and `{documentContentToolName}` together in the same assistant turn. " +
            $"Before calling `{searchToolName}`, rewrite the user's need into a compact retrieval query using likely domain terms, section names, synonyms, and key nouns instead of conversational filler. " +
            $"If the first call to `{searchToolName}` is weak, incomplete, or empty, you may call it again with a materially improved retrieval query. Never repeat the exact same query. " +
            $"Use `{browseToolName}` to list candidate documents or fuzzy-search document titles, paths, and preview contexts when you need DocumentIds, SourceLinks, or more navigational context. " +
            $"Use `{browseTreeToolName}` when the user asks for folder structure, directory navigation, or when you only know part of a path and need to walk the knowledge-base hierarchy. " +
            $"Use `{documentContentToolName}` when you need the original wording of a specific document returned by semantic search, document browsing, or tree browsing. If content is truncated, continue with the returned SuggestedNextStartCharacterIndex or request a larger maxTokens budget up to the stated maximum. Do not repeat the exact same `(document, startCharacterIndex, maxTokens)` request. " +
            "Keep tool use purposeful: prefer at most 2 or 3 materially different retrieval attempts before answering or asking the user to narrow the scope. " +
            "Always cite the source name and source link when knowledge-base content supports your answer.";
    }

    private static string FormatKnowledgeToolPayload(
        string toolName,
        IList<TextSearchProvider.TextSearchResult> results,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        var knowledgeBaseLookup = knowledgeBases
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);

        var payload = new KnowledgeSearchToolPayload
        {
            ToolName = toolName,
            SearchQuery = string.Empty,
            Message = results.Count == 0
                ? "No relevant knowledge base matches were found."
                : "Knowledge base matches retrieved successfully.",
            NextStepInstruction = results.Count == 0
                ? "If semantic search found no matches, refine the query or browse documents for candidate titles and contexts. Do not repeat the exact same semantic search query."
                : "Use the returned knowledge-base results to answer the current question. If you need original document wording, load the document content using the returned DocumentId or SourceLink. If you need to inspect the document hierarchy first, use the document tree tool.",
            CitationInstruction = CitationInstruction,
            ResultCount = results.Count,
            ShouldRetrySameQuery = false,
            CanRetryWithRefinedQuery = results.Count == 0,
            KnowledgeBases = knowledgeBases.Select(x => new KnowledgeSearchToolKnowledgeBase
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            }).ToList(),
            Results = results.Select(result => BuildKnowledgeToolResultItem(result, knowledgeBaseLookup)).ToList()
        };

        return JsonSerializer.Serialize(payload, s_toolJsonOptions);
    }

    private static KnowledgeSearchToolResultItem BuildKnowledgeToolResultItem(
        TextSearchProvider.TextSearchResult result,
        IReadOnlyDictionary<string, KnowledgeBase> knowledgeBaseLookup)
    {
        var rawResult = result.RawRepresentation as Monica.AI.RAG.Models.TextSearchResult;
        var knowledgeBaseId = rawResult?.KnowledgeBaseId;
        knowledgeBaseLookup.TryGetValue(knowledgeBaseId ?? string.Empty, out var knowledgeBase);

        return new KnowledgeSearchToolResultItem
        {
            KnowledgeBaseId = knowledgeBaseId,
            KnowledgeBaseName = knowledgeBase?.Name,
            SourceName = rawResult?.SourceName ?? result.SourceName,
            SourceLink = rawResult?.SourceLink ?? result.SourceLink,
            SectionPath = rawResult?.SectionPath,
            DocumentId = rawResult?.DocumentId,
            ChunkIndex = rawResult?.ChunkIndex,
            Score = rawResult?.Score,
            ScoreKind = rawResult?.ScoreKind.ToString(),
            SimilarityScore = rawResult?.SimilarityScore,
            Content = rawResult?.Text ?? result.Text
        };
    }
}
