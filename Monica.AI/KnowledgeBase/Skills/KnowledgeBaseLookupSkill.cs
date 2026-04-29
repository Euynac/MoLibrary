using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Services.Support;
using Monica.AI.Services.Support;
using Monica.AI.Skills.Abstractions;
using Monica.AI.Skills.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.KnowledgeBase.Skills;

/// <summary>
/// Provides lookup-only scripts for knowledge-base inventory and source documents.
/// </summary>
public sealed class KnowledgeBaseLookupSkill(
    IKnowledgeBaseLookupService lookup,
    IXmlDocumentationService xmlDocumentationService)
    : MoSkill<KnowledgeBaseLookupSkill>(xmlDocumentationService)
{
    private static readonly JsonSerializerOptions _toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc />
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "knowledge-base-lookup",
        "List, browse, and read knowledge-base documents without semantic search.");

    /// <inheritdoc />
    public override IEnumerable<ModuleKey> RequiredModules => [BuiltInModuleKey.KnowledgeBase];

    /// <inheritdoc />
    protected override string Instructions =>
        "Use these scripts when the user wants to discover available knowledge bases, inspect document inventory, " +
        "navigate document hierarchy, or read raw source text. This skill does not perform semantic search; use rag-knowledge when semantic retrieval is needed and available.";

    /// <summary>
    /// Lists all knowledge bases.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized knowledge-base summaries.</returns>
    [MoAITool(
        Name = "list-knowledge-bases",
        Description = "List all knowledge bases, including id, display name, description, and document counts.")]
    public async Task<string> ListAsync(CancellationToken ct = default)
        => JsonSerializer.Serialize(await lookup.ListAsync(ct), _toolJsonOptions);

    /// <summary>
    /// Lists documents in a knowledge base.
    /// </summary>
    /// <param name="kbId">Knowledge base id.</param>
    /// <param name="services">Invocation service provider used to read the current runtime context.</param>
    /// <param name="directoryPath">Optional directory path filter.</param>
    /// <param name="maxResults">Maximum documents to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized document summaries.</returns>
    [MoAITool(
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
        var result = await lookup.BrowseDocumentsAsync(resolvedKbId, directoryPath, maxResults, ct);
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
    [MoAITool(
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
        var result = await lookup.GetDocumentTreeAsync(resolvedKbId, directoryPath, maxDepth, ct);
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
    [MoAITool(
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
        var result = await lookup.GetDocumentContentAsync(
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
}
