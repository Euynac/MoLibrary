using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.AI.Tools;
using Monica.Modules;

namespace Monica.AI.RAG.Tools;

/// <summary>
/// Adds knowledge-base retrieval support to chat agents by wiring the RAG search provider.
/// </summary>
public class KnowledgeSearchToolProvider(
    RAGService ragService,
    IOptions<ModuleRAGOption> ragOptions,
    ILoggerFactory loggerFactory)
    : IAIChatToolProvider
{
    private const string DefaultToolName = "search_knowledge_base";
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
            _logger.LogInformation(
                "Registering knowledge search tool '{ToolName}' for on-demand function calling.",
                searchProviderOptions.FunctionToolName ?? DefaultToolName);

            builder.AddTool(CreateKnowledgeSearchTool(
                searchAdapter,
                searchProviderOptions,
                knowledgeBases));
            builder.EnableAutomaticToolCalling();
            builder.DisableMultipleToolCalling();
            builder.AppendInstructions(BuildKnowledgeToolInstruction(searchProviderOptions.FunctionToolName ?? DefaultToolName));
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
            ? DefaultToolName
            : configuredOptions.FunctionToolName;
        var toolDescription = string.IsNullOrWhiteSpace(configuredOptions?.FunctionToolDescription)
            ? BuildKnowledgeToolDescription(knowledgeBases)
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

    private static string BuildKnowledgeToolDescription(IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        const string defaultDescription =
            "Search the selected knowledge bases for grounded facts, source excerpts, and citation-ready references that help answer the user question.";

        if (knowledgeBases.Count == 0)
        {
            return defaultDescription;
        }

        var descriptionBuilder = new StringBuilder(defaultDescription);
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
        Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>> searchAdapter,
        TextSearchProviderOptions options,
        IReadOnlyList<KnowledgeBase> knowledgeBases)
    {
        ArgumentNullException.ThrowIfNull(searchAdapter);
        ArgumentNullException.ThrowIfNull(options);

        async Task<string> SearchKnowledgeAsync(string userQuestion, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Executing knowledge search tool '{ToolName}' for query: {Query}.",
                options.FunctionToolName ?? DefaultToolName,
                userQuestion);

            var results = await searchAdapter(userQuestion, cancellationToken).ConfigureAwait(false);
            var materialized = results as IList<TextSearchProvider.TextSearchResult> ?? results.ToList();

            _logger.LogInformation(
                "Knowledge search tool '{ToolName}' returned {ResultCount} results.",
                options.FunctionToolName ?? DefaultToolName,
                materialized.Count);

            return options.ContextFormatter is not null
                ? options.ContextFormatter(materialized)
                : FormatKnowledgeToolPayload(
                    options.FunctionToolName ?? DefaultToolName,
                    materialized,
                    knowledgeBases);
        }

        return AIFunctionFactory.Create(
            SearchKnowledgeAsync,
            name: options.FunctionToolName ?? DefaultToolName,
            description: options.FunctionToolDescription ?? BuildKnowledgeToolDescription(knowledgeBases));
    }

    private static string BuildKnowledgeToolInstruction(string toolName)
    {
        return
            $"When the user asks for facts, summaries, or citations from the selected knowledge bases, call the `{toolName}` tool before answering. " +
            "Use the tool again only for materially different follow-up questions that depend on knowledge-base content. " +
            "If the tool returns zero results, or explicitly says not to retry the same query, do not call it again for the same user question. " +
            "Instead, explain that no matching knowledge-base content was found and ask the user to refine the query if needed.";
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
            Message = results.Count == 0
                ? "No relevant knowledge base matches were found."
                : "Knowledge base matches retrieved successfully.",
            NextStepInstruction = results.Count == 0
                ? "Do not call the same knowledge search tool again for this same user question. Tell the user that no matching knowledge-base content was found and ask for a refined query or different knowledge base if needed."
                : "Use the returned knowledge-base results to answer the current question. Do not call the same tool again unless the user asks a materially different follow-up question.",
            CitationInstruction = CitationInstruction,
            ResultCount = results.Count,
            ShouldRetrySameQuery = false,
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
            Content = rawResult?.Text ?? result.Text
        };
    }
}
