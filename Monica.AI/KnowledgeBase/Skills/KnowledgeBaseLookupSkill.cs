using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.Services.Support;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.Skills.Models;
using Monica.Modules;

namespace Monica.AI.KnowledgeBase.Skills;

/// <summary>
/// Provides lookup-only scripts for knowledge-base inventory and source documents.
/// </summary>
internal sealed class KnowledgeBaseLookupSkill(IKnowledgeDocumentQueryService queryService)
    : Skill<KnowledgeBaseLookupSkill>
{
    private static readonly JsonSerializerOptions _toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc />
    public override SkillDefinition Definition { get; } = new(
        "knowledge-base-lookup",
        "List, fuzzy-search, regex-search, browse, and read knowledge-base documents without semantic search.",
        "Use these scripts when the user wants to discover available knowledge bases, inspect document inventory, " +
        "navigate document hierarchy, fuzzy-search document metadata or source content, run targeted regex searches, " +
        "or read raw source text. For hybrid retrieval, use this skill first to find exact files, names, paths, or " +
        "literal/regex matches, then combine those results with rag-knowledge semantic retrieval when RAG is available. " +
        "Prefer search-knowledge-documents before broad browsing when the user gives keywords, filenames, rules, or phrases.");

    /// <inheritdoc />
    public override IReadOnlySet<Type> RequiredModules { get; } =
        new[] { typeof(ModuleKnowledgeBase) }.ToFrozenSet();

    /// <summary>
    /// Gets the optional MCP server definition for external knowledge-base lookup clients.
    /// </summary>
    public override SkillMcpServerDefinition McpServerDefinition { get; } = new(
        "knowledge-base-lookup",
        "List, fuzzy-search, regex-search, browse, and read Monica knowledge-base documents.")
    {
        Title = "Knowledge Base Lookup",
        Instructions =
            "Use fuzzy or regex lookup to locate exact knowledge-base source documents before reading content. " +
            "When semantic RAG search is also available, combine literal lookup results with RAG retrieval.",
        EnabledByDefault = true
    };

    /// <summary>
    /// Lists all knowledge bases.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized knowledge-base summaries.</returns>
    [SkillTool(
        Name = "list-knowledge-bases",
        Description = "List all knowledge bases, including id, display name, description, and document counts.")]
    public async Task<string> ListAsync(CancellationToken ct = default)
        => JsonSerializer.Serialize(await queryService.ListAsync(ct), _toolJsonOptions);

    /// <summary>
    /// Lists documents in a knowledge base.
    /// </summary>
    /// <param name="kbId">Knowledge base id.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="directoryPath">Optional directory path filter.</param>
    /// <param name="maxResults">Maximum documents to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document summaries.</returns>
    [SkillTool(
        Name = "browse-knowledge-documents",
        Description = "List documents in a knowledge base, optionally filtered to a directory path.")]
    public async Task<string> BrowseDocumentsAsync(
        string kbId,
        IServiceProvider services,
        string? directoryPath = null,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var resolvedKbId = ResolveKnowledgeBaseId(kbId, services);
        var result = await queryService.BrowseDocumentsAsync(resolvedKbId, directoryPath, maxResults, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    /// <summary>
    /// Searches documents in a knowledge base using fuzzy metadata/content matching or regular expressions.
    /// </summary>
    /// <param name="kbId">Knowledge base id.</param>
    /// <param name="query">Fuzzy query text or regular expression pattern.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="mode">Search mode: fuzzy or regex.</param>
    /// <param name="directoryPath">Optional directory path filter.</param>
    /// <param name="includeContent">Whether to search source content when it is available.</param>
    /// <param name="maxResults">Maximum results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document search results.</returns>
    [SkillTool(
        Name = "search-knowledge-documents",
        Description = "Fuzzy-search or regex-search knowledge-base document names, paths, directories, and available source content. Use before RAG for hybrid retrieval.")]
    public async Task<string> SearchDocumentsAsync(
        string kbId,
        string query,
        IServiceProvider services,
        string mode = "fuzzy",
        string? directoryPath = null,
        bool includeContent = true,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        var resolvedKbId = ResolveKnowledgeBaseId(kbId, services);
        var searchMode = ParseSearchMode(mode);
        var result = await queryService.SearchDocumentsAsync(
            resolvedKbId,
            query,
            searchMode,
            directoryPath,
            includeContent,
            maxResults,
            ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    /// <summary>
    /// Browses the document tree for a knowledge base.
    /// </summary>
    /// <param name="kbId">Knowledge base id.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="directoryPath">Optional directory path used as the tree root.</param>
    /// <param name="maxDepth">Maximum directory depth.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document tree.</returns>
    [SkillTool(
        Name = "browse-knowledge-document-tree",
        Description = "Browse the directory tree of a knowledge base.")]
    public async Task<string> GetDocumentTreeAsync(
        string kbId,
        IServiceProvider services,
        string? directoryPath = null,
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        var resolvedKbId = ResolveKnowledgeBaseId(kbId, services);
        var result = await queryService.GetDocumentTreeAsync(resolvedKbId, directoryPath, maxDepth, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    /// <summary>
    /// Reads source content for one knowledge-base document.
    /// </summary>
    /// <param name="kbId">Knowledge base id.</param>
    /// <param name="documentId">Document id returned by browsing scripts.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="maxCharacters">Maximum characters to return.</param>
    /// <param name="startCharacterIndex">Character index to start reading from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document content segment.</returns>
    [SkillTool(
        Name = "get-knowledge-document-content",
        Description = "Read a specific document's source content by id.")]
    public async Task<string> GetDocumentContentAsync(
        string kbId,
        string documentId,
        IServiceProvider services,
        int maxCharacters = 8000,
        int startCharacterIndex = 0,
        CancellationToken ct = default)
    {
        var resolvedKbId = ResolveKnowledgeBaseId(kbId, services);
        var result = await queryService.GetDocumentContentAsync(
            resolvedKbId,
            documentId,
            maxCharacters,
            startCharacterIndex,
            ct);

        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    private static string ResolveKnowledgeBaseId(string kbId, IServiceProvider services)
    {
        if (!string.IsNullOrWhiteSpace(kbId))
        {
            return kbId.Trim();
        }

        var runtimeContext = services.GetRequiredService<IAIChatRuntimeContextAccessor>().Current;
        var selection = runtimeContext.GetOrDefault(KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection);
        if (selection?.KnowledgeBaseIds is { Count: 1 } selectedIds)
        {
            return selectedIds[0];
        }

        throw new InvalidOperationException(
            "Knowledge base id is required unless exactly one knowledge base is selected for this chat session.");
    }

    private static KnowledgeDocumentSearchMode ParseSearchMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return KnowledgeDocumentSearchMode.Fuzzy;
        }

        return Enum.TryParse<KnowledgeDocumentSearchMode>(mode, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException("Knowledge document search mode must be 'fuzzy' or 'regex'.", nameof(mode));
    }
}
