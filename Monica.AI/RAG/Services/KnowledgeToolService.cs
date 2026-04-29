using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.RAG.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Builds structured knowledge-tool payloads for semantic search, document browsing, and content retrieval.
/// </summary>
public sealed class KnowledgeToolService(
    RAGService ragService,
    IDocumentIndexStateStore indexStateStore,
    IServiceProvider serviceProvider,
    ITokenCountProvider tokenCountProvider,
    ILogger<KnowledgeToolService> logger)
{
    private const string CitationInstruction =
        "Prefer citing the source name and source link when using these knowledge results.";
    private const int DefaultBrowseDocumentCount = 10;
    private const int MaxBrowseDocumentCount = 20;
    private const int DefaultTreeDepth = 2;
    private const int MaxTreeDepth = 6;
    private const int DefaultTreeEntryCount = 40;
    private const int MaxTreeEntryCount = 120;
    private const int DefaultDocumentContentTokenLimit = 1200;
    private const int MaxDocumentContentTokenLimit = 8000;
    private const int MaxSearchTopK = 10;
    private static readonly TimeSpan DuplicateDocumentRequestWindow = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<string, RecentDocumentContentRequest> _recentDocumentContentRequests = new(
        StringComparer.OrdinalIgnoreCase);

    internal async Task<KnowledgeSearchToolPayload> SearchKnowledgeAsync(
        string toolName,
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases,
        string searchQuery,
        string? userQuestion,
        int? topK,
        CancellationToken ct = default)
    {
        var normalizedQuery = NormalizeText(searchQuery);
        var knowledgeBaseMetadata = BuildKnowledgeBaseMetadata(knowledgeBases);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new KnowledgeSearchToolPayload
            {
                ToolName = toolName,
                SearchQuery = string.Empty,
                UserQuestion = userQuestion,
                Message = "A retrieval query is required before semantic knowledge search can run.",
                NextStepInstruction =
                    "Rewrite the user request into a focused retrieval query, then call the semantic knowledge search tool again. If you need candidate document titles first, use the document browsing tool.",
                CitationInstruction = CitationInstruction,
                ResultCount = 0,
                ShouldRetrySameQuery = false,
                CanRetryWithRefinedQuery = true,
                KnowledgeBases = knowledgeBaseMetadata
            };
        }

        var requestedTopK = topK.GetValueOrDefault();
        var resolvedTopK = requestedTopK <= 0
            ? 0
            : Math.Clamp(requestedTopK, 1, MaxSearchTopK);
        var results = await ragService.SearchAsync(
            normalizedQuery,
            knowledgeBases.Select(static kb => kb.Id),
            resolvedTopK,
            ct);

        return new KnowledgeSearchToolPayload
        {
            ToolName = toolName,
            SearchQuery = normalizedQuery,
            UserQuestion = userQuestion,
            Message = results.Count == 0
                ? "No semantic knowledge matches were found for the current retrieval query."
                : "Semantic knowledge matches retrieved successfully.",
            NextStepInstruction = results.Count == 0
                ? "You may retry with a materially improved retrieval query or use the document browsing tool to find likely document titles and preview contexts. Do not repeat the exact same query."
                : "Use these grounded results to answer the user. If you need the original document wording or a longer excerpt, use the document content tool with the returned DocumentId or SourceLink.",
            CitationInstruction = CitationInstruction,
            ResultCount = results.Count,
            ShouldRetrySameQuery = false,
            CanRetryWithRefinedQuery = results.Count == 0 || results.Count < Math.Min(2, resolvedTopK == 0 ? 5 : resolvedTopK),
            KnowledgeBases = knowledgeBaseMetadata,
            Results = results.Select(result => BuildKnowledgeSearchResultItem(result, knowledgeBases)).ToList()
        };
    }

    internal async Task<KnowledgeDocumentBrowseToolPayload> BrowseDocumentsAsync(
        string toolName,
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases,
        string? query,
        int? maxDocuments,
        CancellationToken ct = default)
    {
        var knowledgeBaseMetadata = BuildKnowledgeBaseMetadata(knowledgeBases);
        var entries = await LoadDocumentEntriesAsync(knowledgeBases, ct);
        var resolvedLimit = ResolvePositiveValue(maxDocuments, DefaultBrowseDocumentCount, MaxBrowseDocumentCount);

        if (entries.Count == 0)
        {
            return new KnowledgeDocumentBrowseToolPayload
            {
                ToolName = toolName,
                Mode = "list",
                Query = NormalizeText(query),
                Message = "No indexed knowledge-base documents are currently available in the selected knowledge bases.",
                NextStepInstruction =
                    "Answer without document browsing, or ask the user to index documents into the selected knowledge base first.",
                ResultCount = 0,
                SearchContextAvailable = false,
                KnowledgeBases = knowledgeBaseMetadata
            };
        }

        var normalizedQuery = NormalizeText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var listedDocuments = entries
                .OrderBy(static entry => entry.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.DocumentId, StringComparer.OrdinalIgnoreCase)
                .Take(resolvedLimit)
                .Select(entry => BuildBrowseResultItem(entry))
                .ToList();

            return new KnowledgeDocumentBrowseToolPayload
            {
                ToolName = toolName,
                Mode = "list",
                Message = "Indexed knowledge-base documents listed successfully.",
                NextStepInstruction =
                    "Choose a relevant DocumentId or SourceLink and call the document content tool when you need the original wording of a specific document.",
                ResultCount = listedDocuments.Count,
                SearchContextAvailable = false,
                KnowledgeBases = knowledgeBaseMetadata,
                Results = listedDocuments
            };
        }

        var combinedResults = new Dictionary<string, KnowledgeDocumentBrowseResultItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var markdownHit in await SearchMarkdownDocumentsAsync(entries, normalizedQuery, ct))
        {
            combinedResults[BuildBrowseResultKey(markdownHit)] = markdownHit;
        }

        foreach (var fallbackHit in SearchDocumentMetadata(entries, normalizedQuery))
        {
            combinedResults.TryAdd(BuildBrowseResultKey(fallbackHit), fallbackHit);
        }

        var finalResults = combinedResults.Values
            .OrderByDescending(result => result.Score ?? 0)
            .ThenBy(result => result.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.DocumentId, StringComparer.OrdinalIgnoreCase)
            .Take(resolvedLimit)
            .ToList();

        return new KnowledgeDocumentBrowseToolPayload
        {
            ToolName = toolName,
            Mode = "fuzzy_search",
            Query = normalizedQuery,
            Message = finalResults.Count == 0
                ? "No matching knowledge documents were found for the current fuzzy search query."
                : "Matching knowledge documents were found successfully.",
            NextStepInstruction = finalResults.Count == 0
                ? "Refine the document query, or use semantic knowledge search if you are looking for concept-level relevance instead of document titles."
                : "Pick the most relevant DocumentId or SourceLink from these candidates. Then call the document content tool if you need the original source text.",
            ResultCount = finalResults.Count,
            SearchContextAvailable = finalResults.Any(result => !string.IsNullOrWhiteSpace(result.PreviewText)),
            KnowledgeBases = knowledgeBaseMetadata,
            Results = finalResults
        };
    }

    internal async Task<KnowledgeDocumentTreeToolPayload> BrowseDocumentTreeAsync(
        string toolName,
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases,
        string? knowledgeBaseId,
        string? directoryPath,
        int? maxDepth,
        int? maxEntries,
        CancellationToken ct = default)
    {
        var knowledgeBaseMetadata = BuildKnowledgeBaseMetadata(knowledgeBases);
        var requestedKnowledgeBaseId = NormalizeText(knowledgeBaseId);
        var requestedDirectoryPath = NormalizeDirectoryPath(directoryPath);
        var resolvedDepth = ResolvePositiveValue(maxDepth, DefaultTreeDepth, MaxTreeDepth);
        var resolvedEntryLimit = ResolvePositiveValue(maxEntries, DefaultTreeEntryCount, MaxTreeEntryCount);

        var entries = await LoadDocumentEntriesAsync(knowledgeBases, ct);
        if (!string.IsNullOrWhiteSpace(requestedKnowledgeBaseId))
        {
            entries = entries
                .Where(entry => string.Equals(entry.KnowledgeBaseId, requestedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (entries.Count == 0)
        {
            return new KnowledgeDocumentTreeToolPayload
            {
                ToolName = toolName,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                DirectoryPath = requestedDirectoryPath,
                RequestedMaxDepth = resolvedDepth,
                RequestedMaxEntries = resolvedEntryLimit,
                Message = "No indexed knowledge-base documents are currently available for directory browsing.",
                NextStepInstruction =
                    "Use semantic search or fuzzy document browsing if you already know what to look for, or ask the user to index documents first.",
                DirectoryFound = string.IsNullOrWhiteSpace(requestedDirectoryPath),
                KnowledgeBases = knowledgeBaseMetadata
            };
        }

        var treeEntries = BuildTreeEntries(
            entries,
            requestedDirectoryPath,
            resolvedDepth,
            resolvedEntryLimit,
            out var directoryFound,
            out var hasMoreEntries);

        return new KnowledgeDocumentTreeToolPayload
        {
            ToolName = toolName,
            RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
            DirectoryPath = requestedDirectoryPath,
            RequestedMaxDepth = resolvedDepth,
            RequestedMaxEntries = resolvedEntryLimit,
            Message = !directoryFound
                ? "No matching directory or document path was found in the selected knowledge bases."
                : treeEntries.Count == 0
                    ? "The requested directory exists, but no entries were returned within the current depth and entry limits."
                    : "Knowledge-base directory structure loaded successfully.",
            NextStepInstruction = !directoryFound
                ? "Refine the directory path, use fuzzy document browsing to find the correct path, or use semantic search if you are looking for a concept instead of a file path."
                : treeEntries.Any(entry => string.Equals(entry.EntryType, "document", StringComparison.OrdinalIgnoreCase))
                    ? "Use the returned DocumentId or SourceLink to call the document content tool. To navigate deeper, call this tree tool again with one of the returned directory paths."
                    : "Call this tree tool again with one of the returned directory paths to inspect deeper levels, or use fuzzy document browsing if you need contextual matches.",
            DirectoryFound = directoryFound,
            HasMoreEntries = hasMoreEntries,
            DirectoryCount = treeEntries.Count(entry => string.Equals(entry.EntryType, "directory", StringComparison.OrdinalIgnoreCase)),
            DocumentCount = treeEntries.Count(entry => string.Equals(entry.EntryType, "document", StringComparison.OrdinalIgnoreCase)),
            KnowledgeBases = knowledgeBaseMetadata,
            Entries = treeEntries
        };
    }

    internal async Task<KnowledgeDocumentContentToolPayload> GetDocumentContentAsync(
        string toolName,
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases,
        string? documentId,
        string? sourceLink,
        string? knowledgeBaseId,
        int? maxTokens,
        int? startCharacterIndex,
        CancellationToken ct = default)
    {
        var requestedDocumentId = NormalizeDocumentReference(documentId);
        var requestedSourceLink = NormalizeDocumentReference(sourceLink);
        var requestedKnowledgeBaseId = NormalizeText(knowledgeBaseId);
        var resolvedStartCharacterIndex = Math.Max(0, startCharacterIndex ?? 0);
        var resolvedMaxTokens = ResolvePositiveValue(
            maxTokens,
            DefaultDocumentContentTokenLimit,
            MaxDocumentContentTokenLimit);
        var tokenBudgetClamped = maxTokens is > MaxDocumentContentTokenLimit;
        var tokenBudgetNote = tokenBudgetClamped
            ? $" The requested token budget {maxTokens} exceeds the current maximum {MaxDocumentContentTokenLimit}, so {resolvedMaxTokens} was used."
            : string.Empty;

        if (string.IsNullOrWhiteSpace(requestedDocumentId) && string.IsNullOrWhiteSpace(requestedSourceLink))
        {
            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = "A DocumentId or SourceLink is required to load original document content.",
                NextStepInstruction =
                    "Use the document browsing tool or semantic search results to pick a specific DocumentId or SourceLink first.",
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated
            };
        }

        var entries = await LoadDocumentEntriesAsync(knowledgeBases, ct);
        if (!string.IsNullOrWhiteSpace(requestedKnowledgeBaseId))
        {
            entries = entries
                .Where(entry => string.Equals(entry.KnowledgeBaseId, requestedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var matchedEntries = entries
            .Where(entry => MatchesDocumentReference(entry, requestedDocumentId, requestedSourceLink))
            .OrderBy(entry => entry.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matchedEntries.Count == 0)
        {
            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = "No matching knowledge document was found for the supplied DocumentId or SourceLink.",
                NextStepInstruction =
                    "Call the document browsing tool to discover the correct DocumentId or SourceLink, or refine the requested knowledge base.",
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated
            };
        }

        if (matchedEntries.Count > 1)
        {
            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = "Multiple knowledge documents matched the supplied DocumentId or SourceLink.",
                NextStepInstruction =
                    "Choose the intended knowledge base from the returned candidates, then call the document content tool again with the matching KnowledgeBaseId.",
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated,
                Candidates = matchedEntries
                    .Take(DefaultBrowseDocumentCount)
                    .Select(entry => BuildBrowseResultItem(entry))
                    .ToList()
            };
        }

        var matchedEntry = matchedEntries[0];
        var duplicateRequestKey = BuildDocumentContentRequestKey(
            matchedEntry,
            resolvedStartCharacterIndex,
            resolvedMaxTokens);
        if (TryGetDuplicateDocumentContentRequest(duplicateRequestKey, out var recentRequest))
        {
            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = "The same document segment was already returned in a recent tool call.",
                NextStepInstruction =
                    recentRequest.WasTruncated
                        ? $"Use the previous result. To continue, call the document content tool again with startCharacterIndex={recentRequest.ReturnedEndCharacterIndex}, or increase maxTokens up to {MaxDocumentContentTokenLimit} if you want a larger segment in one call."
                        : "Use the previous result or choose a different document. Repeating the same document segment immediately adds no new information.",
                DuplicateRequestBlocked = true,
                ContentTruncated = recentRequest.WasTruncated,
                CanContinue = recentRequest.WasTruncated,
                SuggestedNextStartCharacterIndex = recentRequest.WasTruncated
                    ? recentRequest.ReturnedEndCharacterIndex
                    : null,
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated,
                ReturnedStartCharacterIndex = recentRequest.ReturnedStartCharacterIndex,
                ReturnedEndCharacterIndex = recentRequest.ReturnedEndCharacterIndex,
                Candidates =
                [
                    BuildBrowseResultItem(matchedEntry)
                ]
            };
        }

        var content = await LoadDocumentContentAsync(matchedEntry, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = $"The requested knowledge document was found, but its original content is not currently available.{tokenBudgetNote}",
                NextStepInstruction =
                    "Answer from the existing search results or ask the user to re-index the document before requesting the original content again.",
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated
            };
        }

        if (resolvedStartCharacterIndex >= content.Length)
        {
            var originalMetrics = tokenCountProvider.CountTokens(content);

            return new KnowledgeDocumentContentToolPayload
            {
                ToolName = toolName,
                RequestedDocumentId = requestedDocumentId,
                RequestedSourceLink = requestedSourceLink,
                RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
                RequestedMaxTokens = resolvedMaxTokens,
                RawRequestedMaxTokens = maxTokens,
                EffectiveMaxTokens = resolvedMaxTokens,
                TokenBudgetClamped = tokenBudgetClamped,
                MaxSupportedTokens = MaxDocumentContentTokenLimit,
                RequestedStartCharacterIndex = resolvedStartCharacterIndex,
                Message = $"The requested startCharacterIndex is beyond the end of the document.{tokenBudgetNote}",
                NextStepInstruction =
                    $"Use a smaller startCharacterIndex. The document length is {content.Length} characters in total.",
                TokenCountProviderId = tokenCountProvider.ProviderId,
                TokenCountProviderName = tokenCountProvider.DisplayName,
                IsEstimatedTokenCount = tokenCountProvider.IsEstimated,
                OriginalCharacterCount = originalMetrics.CharacterCount,
                OriginalTokenCount = originalMetrics.TokenCount,
                ReturnedStartCharacterIndex = resolvedStartCharacterIndex,
                ReturnedEndCharacterIndex = resolvedStartCharacterIndex,
                Document = new KnowledgeDocumentContentResultItem
                {
                    KnowledgeBaseId = matchedEntry.KnowledgeBaseId,
                    KnowledgeBaseName = matchedEntry.KnowledgeBaseName,
                    DocumentId = matchedEntry.DocumentId,
                    SourceName = matchedEntry.SourceName,
                    SourceLink = matchedEntry.SourceLink,
                    SourceKind = matchedEntry.SourceKind,
                    SourceGroupKey = matchedEntry.SourceGroupKey,
                    Status = matchedEntry.Status.ToString(),
                    ChunkCount = matchedEntry.ChunkCount,
                    IndexedAt = matchedEntry.IndexedAt,
                    Content = string.Empty
                }
            };
        }

        var remainingContent = content[resolvedStartCharacterIndex..];
        var truncation = tokenCountProvider.TruncateToMaxTokens(remainingContent, resolvedMaxTokens);
        var returnedStartCharacterIndex = resolvedStartCharacterIndex;
        var returnedEndCharacterIndex = returnedStartCharacterIndex + truncation.Text.Length;
        var trailingContent = returnedEndCharacterIndex >= content.Length
            ? string.Empty
            : content[returnedEndCharacterIndex..];
        var remainingMetrics = tokenCountProvider.CountTokens(trailingContent);

        RememberDocumentContentRequest(
            duplicateRequestKey,
            new RecentDocumentContentRequest(
                DateTimeOffset.UtcNow,
                returnedStartCharacterIndex,
                returnedEndCharacterIndex,
                truncation.WasTruncated));

        return new KnowledgeDocumentContentToolPayload
        {
            ToolName = toolName,
            RequestedDocumentId = requestedDocumentId,
            RequestedSourceLink = requestedSourceLink,
            RequestedKnowledgeBaseId = requestedKnowledgeBaseId,
            RequestedMaxTokens = resolvedMaxTokens,
            RawRequestedMaxTokens = maxTokens,
            EffectiveMaxTokens = resolvedMaxTokens,
            TokenBudgetClamped = tokenBudgetClamped,
            MaxSupportedTokens = MaxDocumentContentTokenLimit,
            RequestedStartCharacterIndex = resolvedStartCharacterIndex,
            Message = truncation.WasTruncated
                ? $"Knowledge document content loaded successfully and truncated to fit the token budget.{tokenBudgetNote}"
                : resolvedStartCharacterIndex > 0
                    ? $"Knowledge document continuation loaded successfully.{tokenBudgetNote}"
                    : $"Knowledge document content loaded successfully.{tokenBudgetNote}",
            NextStepInstruction = truncation.WasTruncated
                ? $"More content remains. Call the document content tool again with the same document and startCharacterIndex={returnedEndCharacterIndex} to continue from the next segment, or increase maxTokens up to {MaxDocumentContentTokenLimit} for a larger excerpt in one call."
                : "Use the returned original content to answer the user. Prefer quoting only the minimal excerpt needed.",
            ContentTruncated = truncation.WasTruncated,
            CanContinue = truncation.WasTruncated,
            SuggestedNextStartCharacterIndex = truncation.WasTruncated
                ? returnedEndCharacterIndex
                : null,
            TokenCountProviderId = tokenCountProvider.ProviderId,
            TokenCountProviderName = tokenCountProvider.DisplayName,
            IsEstimatedTokenCount = tokenCountProvider.IsEstimated,
            OriginalCharacterCount = truncation.Original.CharacterCount,
            OriginalTokenCount = truncation.Original.TokenCount,
            ReturnedCharacterCount = truncation.Returned.CharacterCount,
            ReturnedTokenCount = truncation.Returned.TokenCount,
            ReturnedStartCharacterIndex = returnedStartCharacterIndex,
            ReturnedEndCharacterIndex = returnedEndCharacterIndex,
            RemainingCharacterCount = trailingContent.Length,
            RemainingTokenCount = remainingMetrics.TokenCount,
            Document = new KnowledgeDocumentContentResultItem
            {
                KnowledgeBaseId = matchedEntry.KnowledgeBaseId,
                KnowledgeBaseName = matchedEntry.KnowledgeBaseName,
                DocumentId = matchedEntry.DocumentId,
                SourceName = matchedEntry.SourceName,
                SourceLink = matchedEntry.SourceLink,
                SourceKind = matchedEntry.SourceKind,
                SourceGroupKey = matchedEntry.SourceGroupKey,
                Status = matchedEntry.Status.ToString(),
                ChunkCount = matchedEntry.ChunkCount,
                IndexedAt = matchedEntry.IndexedAt,
                Content = truncation.Text
            }
        };
    }

    private async Task<List<KnowledgeDocumentEntry>> LoadDocumentEntriesAsync(
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases,
        CancellationToken ct)
    {
        var entries = new List<KnowledgeDocumentEntry>();

        foreach (var knowledgeBase in knowledgeBases)
        {
            ct.ThrowIfCancellationRequested();

            var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBase.Id, ct);
            entries.AddRange(states.Select(state => new KnowledgeDocumentEntry(
                knowledgeBase.Id,
                knowledgeBase.Name,
                state.DocumentPath,
                state.DocumentName,
                state.DocumentPath,
                state.SourceKind ?? KnowledgeDocumentSourceKinds.Unknown,
                state.SourceGroupKey,
                state.Status,
                state.ChunkCount,
                state.IndexedAt)));
        }

        return entries;
    }

    private async Task<List<KnowledgeDocumentBrowseResultItem>> SearchMarkdownDocumentsAsync(
        IReadOnlyList<KnowledgeDocumentEntry> entries,
        string query,
        CancellationToken ct)
    {
        var markdownSearchService = serviceProvider.GetService<IMarkdownDocumentSearcher>();
        if (markdownSearchService is null)
        {
            return [];
        }

        var markdownEntriesByGroup = entries
            .Where(entry =>
                string.Equals(entry.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.SourceGroupKey))
            .GroupBy(entry => entry.SourceGroupKey!, StringComparer.OrdinalIgnoreCase);

        var results = new List<KnowledgeDocumentBrowseResultItem>();

        foreach (var group in markdownEntriesByGroup)
        {
            ct.ThrowIfCancellationRequested();

            IReadOnlyList<MarkdownDocumentSearchResult> groupHits;
            try
            {
                groupHits = await markdownSearchService.SearchAsync(
                    new MarkdownDocumentSearchRequest(query, group.Key, IncludeAllKnowledgeBases: false),
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Failed to search markdown documents for group '{GroupKey}'.", group.Key);
                continue;
            }

            var groupedEntries = group
                .GroupBy(entry => entry.DocumentId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray(), StringComparer.OrdinalIgnoreCase);

            foreach (var hit in groupHits)
            {
                if (!groupedEntries.TryGetValue(hit.DocumentRelativePath, out var matchedEntries))
                {
                    continue;
                }

                foreach (var matchedEntry in matchedEntries)
                {
                    results.Add(BuildBrowseResultItem(matchedEntry, hit.Score, hit));
                }
            }
        }

        return results;
    }

    private static IEnumerable<KnowledgeDocumentBrowseResultItem> SearchDocumentMetadata(
        IEnumerable<KnowledgeDocumentEntry> entries,
        string query)
    {
        return entries
            .Select(entry => new
            {
                Entry = entry,
                Score = ScoreDocumentEntry(entry, query)
            })
            .Where(static candidate => candidate.Score > 0)
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(candidate => candidate.Entry.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Entry.SourceName, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => BuildBrowseResultItem(candidate.Entry, candidate.Score));
    }

    private async Task<string?> LoadDocumentContentAsync(KnowledgeDocumentEntry entry, CancellationToken ct)
    {
        var content = await ragService.GetDocumentSourceContentAsync(entry.KnowledgeBaseId, entry.DocumentId, ct);
        if (!string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        if (!string.Equals(entry.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var markdownService = serviceProvider.GetService<IMarkdownDocumentCatalog>();
        if (markdownService is null)
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(entry.SourceGroupKey))
            {
                var groupDocuments = await markdownService.GetDocumentsAsync(entry.SourceGroupKey);
                var matchedDocument = groupDocuments.FirstOrDefault(document =>
                    string.Equals(document.RelativePath, entry.DocumentId, StringComparison.OrdinalIgnoreCase));
                if (matchedDocument is not null)
                {
                    return await markdownService.GetDocumentContentAsync(matchedDocument);
                }
            }

            var document = await markdownService.GetDocumentByPathAsync(entry.DocumentId);
            return await markdownService.GetDocumentContentAsync(document);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or FileNotFoundException)
        {
            logger.LogDebug(
                ex,
                "Fallback markdown content resolution failed for document '{DocumentId}' in KB '{KnowledgeBaseId}'.",
                entry.DocumentId,
                entry.KnowledgeBaseId);
            return null;
        }
    }

    private static KnowledgeSearchToolResultItem BuildKnowledgeSearchResultItem(
        TextSearchResult result,
        IReadOnlyList<KnowledgeBaseModel> knowledgeBases)
    {
        var knowledgeBase = knowledgeBases.FirstOrDefault(kb =>
            string.Equals(kb.Id, result.KnowledgeBaseId, StringComparison.OrdinalIgnoreCase));

        return new KnowledgeSearchToolResultItem
        {
            KnowledgeBaseId = result.KnowledgeBaseId,
            KnowledgeBaseName = knowledgeBase?.Name,
            SourceName = result.SourceName,
            SourceLink = result.SourceLink,
            SectionPath = result.SectionPath,
            DocumentId = result.DocumentId,
            ChunkIndex = result.ChunkIndex,
            Score = result.Score,
            ScoreKind = result.ScoreKind.ToString(),
            SimilarityScore = result.SimilarityScore,
            Content = result.Text
        };
    }

    private static IReadOnlyList<KnowledgeSearchToolKnowledgeBase> BuildKnowledgeBaseMetadata(
        IEnumerable<KnowledgeBaseModel> knowledgeBases)
    {
        return knowledgeBases
            .Select(knowledgeBase => new KnowledgeSearchToolKnowledgeBase
            {
                Id = knowledgeBase.Id,
                Name = knowledgeBase.Name,
                Description = knowledgeBase.Description
            })
            .ToList();
    }

    private static KnowledgeDocumentBrowseResultItem BuildBrowseResultItem(
        KnowledgeDocumentEntry entry,
        double? score = null,
        MarkdownDocumentSearchResult? markdownSearchResult = null)
    {
        return new KnowledgeDocumentBrowseResultItem
        {
            KnowledgeBaseId = entry.KnowledgeBaseId,
            KnowledgeBaseName = entry.KnowledgeBaseName,
            DocumentId = entry.DocumentId,
            SourceName = entry.SourceName,
            SourceLink = entry.SourceLink,
            SourceKind = entry.SourceKind,
            SourceGroupKey = entry.SourceGroupKey,
            Status = entry.Status.ToString(),
            ChunkCount = entry.ChunkCount,
            IndexedAt = entry.IndexedAt,
            Score = score,
            SectionTitle = markdownSearchResult?.SectionTitle,
            PreviewText = markdownSearchResult?.PreviewText,
            AnchorId = markdownSearchResult?.AnchorId,
            MatchedText = markdownSearchResult?.Locator.MatchedText,
            PrefixContext = markdownSearchResult?.Locator.PrefixContext,
            SuffixContext = markdownSearchResult?.Locator.SuffixContext
        };
    }

    private static string BuildBrowseResultKey(KnowledgeDocumentBrowseResultItem item)
        => $"{item.KnowledgeBaseId}::{item.DocumentId}";

    private static IReadOnlyList<KnowledgeDocumentTreeEntry> BuildTreeEntries(
        IReadOnlyList<KnowledgeDocumentEntry> entries,
        string directoryPath,
        int maxDepth,
        int maxEntries,
        out bool directoryFound,
        out bool hasMoreEntries)
    {
        var directoryAggregates = new Dictionary<string, TreeDirectoryAggregate>(StringComparer.OrdinalIgnoreCase);
        var treeEntries = new List<KnowledgeDocumentTreeEntry>();
        directoryFound = string.IsNullOrWhiteSpace(directoryPath);

        foreach (var knowledgeBaseGroup in entries
                     .GroupBy(entry => new { entry.KnowledgeBaseId, entry.KnowledgeBaseName })
                     .OrderBy(group => group.Key.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase))
        {
            var exactDocumentMatches = knowledgeBaseGroup
                .Where(entry => string.Equals(entry.DocumentId, directoryPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.DocumentId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (exactDocumentMatches.Count > 0)
            {
                directoryFound = true;
                treeEntries.AddRange(exactDocumentMatches.Select(entry => new KnowledgeDocumentTreeEntry
                {
                    KnowledgeBaseId = entry.KnowledgeBaseId,
                    KnowledgeBaseName = entry.KnowledgeBaseName,
                    EntryType = "document",
                    Name = entry.SourceName,
                    Path = entry.DocumentId,
                    Depth = 0,
                    DocumentId = entry.DocumentId,
                    SourceName = entry.SourceName,
                    SourceLink = entry.SourceLink,
                    SourceKind = entry.SourceKind,
                    SourceGroupKey = entry.SourceGroupKey,
                    Status = entry.Status.ToString(),
                    ChunkCount = entry.ChunkCount,
                    IndexedAt = entry.IndexedAt
                }));
            }

            foreach (var entry in knowledgeBaseGroup)
            {
                if (!TryGetRelativeDocumentPath(entry.DocumentId, directoryPath, out var relativeDocumentPath))
                {
                    continue;
                }

                directoryFound = true;

                var pathSegments = relativeDocumentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length == 0)
                {
                    continue;
                }

                var directoryDepth = Math.Min(pathSegments.Length - 1, maxDepth);
                for (var segmentIndex = 0; segmentIndex < directoryDepth; segmentIndex++)
                {
                    var fullDirectoryPath = CombinePath(directoryPath, pathSegments.Take(segmentIndex + 1));
                    var aggregateKey = $"{entry.KnowledgeBaseId}::{fullDirectoryPath}";

                    if (!directoryAggregates.TryGetValue(aggregateKey, out var aggregate))
                    {
                        aggregate = new TreeDirectoryAggregate(
                            entry.KnowledgeBaseId,
                            entry.KnowledgeBaseName,
                            pathSegments[segmentIndex],
                            fullDirectoryPath,
                            segmentIndex);
                        directoryAggregates[aggregateKey] = aggregate;
                    }

                    aggregate.DocumentCount++;
                }

                var documentDepth = pathSegments.Length - 1;
                if (documentDepth > maxDepth)
                {
                    continue;
                }

                treeEntries.Add(new KnowledgeDocumentTreeEntry
                {
                    KnowledgeBaseId = entry.KnowledgeBaseId,
                    KnowledgeBaseName = entry.KnowledgeBaseName,
                    EntryType = "document",
                    Name = entry.SourceName,
                    Path = entry.DocumentId,
                    Depth = documentDepth,
                    DocumentId = entry.DocumentId,
                    SourceName = entry.SourceName,
                    SourceLink = entry.SourceLink,
                    SourceKind = entry.SourceKind,
                    SourceGroupKey = entry.SourceGroupKey,
                    Status = entry.Status.ToString(),
                    ChunkCount = entry.ChunkCount,
                    IndexedAt = entry.IndexedAt
                });
            }
        }

        treeEntries.AddRange(directoryAggregates.Values.Select(aggregate => new KnowledgeDocumentTreeEntry
        {
            KnowledgeBaseId = aggregate.KnowledgeBaseId,
            KnowledgeBaseName = aggregate.KnowledgeBaseName,
            EntryType = "directory",
            Name = aggregate.Name,
            Path = aggregate.Path,
            Depth = aggregate.Depth,
            DocumentCount = aggregate.DocumentCount
        }));

        var orderedEntries = treeEntries
            .DistinctBy(entry => $"{entry.KnowledgeBaseId}::{entry.EntryType}::{entry.Path}")
            .OrderBy(entry => entry.KnowledgeBaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Depth)
            .ThenBy(entry => string.Equals(entry.EntryType, "directory", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        hasMoreEntries = orderedEntries.Count > maxEntries;
        return hasMoreEntries
            ? orderedEntries.Take(maxEntries).ToList()
            : orderedEntries;
    }

    private static bool MatchesDocumentReference(
        KnowledgeDocumentEntry entry,
        string? requestedDocumentId,
        string? requestedSourceLink)
    {
        if (!string.IsNullOrWhiteSpace(requestedDocumentId)
            && string.Equals(entry.DocumentId, requestedDocumentId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(requestedSourceLink)
               && string.Equals(entry.SourceLink, requestedSourceLink, StringComparison.OrdinalIgnoreCase);
    }

    private static double ScoreDocumentEntry(KnowledgeDocumentEntry entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (string.Equals(entry.SourceName, query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (string.Equals(entry.DocumentId, query, StringComparison.OrdinalIgnoreCase))
        {
            return 95;
        }

        if (ContainsIgnoreCase(entry.SourceName, query))
        {
            return 90;
        }

        if (ContainsIgnoreCase(entry.DocumentId, query))
        {
            return 80;
        }

        if (ContainsIgnoreCase(entry.SourceLink, query))
        {
            return 70;
        }

        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length == 0)
        {
            return 0;
        }

        var haystack = $"{entry.SourceName} {entry.DocumentId} {entry.SourceLink}";
        var matchedKeywordCount = keywords.Count(keyword => ContainsIgnoreCase(haystack, keyword));
        if (matchedKeywordCount == 0)
        {
            return 0;
        }

        return 40 + (matchedKeywordCount * 20d / keywords.Length);
    }

    private bool TryGetDuplicateDocumentContentRequest(
        string requestKey,
        out RecentDocumentContentRequest recentRequest)
    {
        if (!_recentDocumentContentRequests.TryGetValue(requestKey, out var cachedRequest))
        {
            recentRequest = new RecentDocumentContentRequest(default, 0, 0, false);
            return false;
        }

        recentRequest = cachedRequest;

        if (DateTimeOffset.UtcNow - recentRequest.RequestedAt <= DuplicateDocumentRequestWindow)
        {
            return true;
        }

        _recentDocumentContentRequests.TryRemove(requestKey, out _);
        recentRequest = new RecentDocumentContentRequest(default, 0, 0, false);
        return false;
    }

    private void RememberDocumentContentRequest(string requestKey, RecentDocumentContentRequest request)
    {
        _recentDocumentContentRequests[requestKey] = request;
    }

    private static string BuildDocumentContentRequestKey(
        KnowledgeDocumentEntry entry,
        int startCharacterIndex,
        int maxTokens)
        => $"{entry.KnowledgeBaseId}::{entry.DocumentId}::{startCharacterIndex}::{maxTokens}";

    private static int ResolvePositiveValue(int? requestedValue, int defaultValue, int maxValue)
    {
        if (requestedValue is not > 0)
        {
            return defaultValue;
        }

        return Math.Clamp(requestedValue.Value, 1, maxValue);
    }

    private static bool ContainsIgnoreCase(string source, string value)
        => source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeDocumentReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('\\', '/').Trim();
    }

    private static string NormalizeDirectoryPath(string? value)
        => NormalizeDocumentReference(value).Trim('/');

    private static bool TryGetRelativeDocumentPath(
        string documentPath,
        string directoryPath,
        out string relativeDocumentPath)
    {
        relativeDocumentPath = string.Empty;

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            relativeDocumentPath = documentPath;
            return true;
        }

        if (string.Equals(documentPath, directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = directoryPath + "/";
        if (!documentPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativeDocumentPath = documentPath[prefix.Length..];
        return !string.IsNullOrWhiteSpace(relativeDocumentPath);
    }

    private static string CombinePath(string basePath, IEnumerable<string> additionalSegments)
    {
        var extra = string.Join('/', additionalSegments);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return extra;
        }

        return string.IsNullOrWhiteSpace(extra)
            ? basePath
            : $"{basePath}/{extra}";
    }

    private sealed record KnowledgeDocumentEntry(
        string KnowledgeBaseId,
        string KnowledgeBaseName,
        string DocumentId,
        string SourceName,
        string SourceLink,
        string SourceKind,
        string? SourceGroupKey,
        DocumentStatus Status,
        int ChunkCount,
        DateTimeOffset? IndexedAt);

    private sealed record RecentDocumentContentRequest(
        DateTimeOffset RequestedAt,
        int ReturnedStartCharacterIndex,
        int ReturnedEndCharacterIndex,
        bool WasTruncated);

    private sealed class TreeDirectoryAggregate(
        string knowledgeBaseId,
        string knowledgeBaseName,
        string name,
        string path,
        int depth)
    {
        public string KnowledgeBaseId { get; } = knowledgeBaseId;
        public string KnowledgeBaseName { get; } = knowledgeBaseName;
        public string Name { get; } = name;
        public string Path { get; } = path;
        public int Depth { get; } = depth;
        public int DocumentCount { get; set; }
    }
}
