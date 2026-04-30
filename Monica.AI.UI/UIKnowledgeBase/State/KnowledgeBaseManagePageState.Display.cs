using Monica.AI.KnowledgeBase.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIKnowledgeBase.State;

public sealed partial class KnowledgeBaseManagePageState
{
    /// <summary>
    /// Checks whether one knowledge base has a RAG embedding binding.
    /// </summary>
    public static bool IsRagEnabled(KnowledgeBaseModel knowledgeBase)
        => !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
           && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName);

    /// <summary>
    /// Resolves the display label of one provider.
    /// </summary>
    public string GetProviderDisplayLabel(string? providerId, string? modelName = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return string.Empty;
        }

        var provider = _providerFactory.GetProvider(providerId);
        return provider?.DisplayName ?? providerId.Trim();
    }

    /// <summary>
    /// Resolves one knowledge-base embedding display string.
    /// </summary>
    public string GetKnowledgeBaseModelDisplay(KnowledgeBaseModel knowledgeBase)
    {
        if (!IsRagEnabled(knowledgeBase))
        {
            return string.Empty;
        }

        return $"{knowledgeBase.EmbeddingModelName} ({GetProviderDisplayLabel(knowledgeBase.EmbeddingProviderId, knowledgeBase.EmbeddingModelName)})";
    }

    /// <summary>
    /// Resolves the aggregate sidebar statistics for one knowledge base.
    /// </summary>
    public string GetKnowledgeBaseStatsDisplay(KnowledgeBaseModel knowledgeBase)
        => IsRagEnabled(knowledgeBase)
            ? _localizer["RAG:Debug:KBSidebar:Stats", knowledgeBase.DocumentCount, knowledgeBase.ChunkCount].Value
            : _localizer["RAG:KnowledgeBase:DocsCount", knowledgeBase.DocumentCount].Value;

    /// <summary>
    /// Resolves display text for document status.
    /// </summary>
    public string GetDocumentStatusDisplay(DocumentStatus status)
        => status switch
        {
            DocumentStatus.Pending => _localizer["RAG:DocumentQueue:Status:Pending"],
            DocumentStatus.Indexing => _localizer["RAG:DocumentQueue:Status:Indexing"],
            DocumentStatus.Done => _localizer["RAG:DocumentQueue:Status:Done"],
            DocumentStatus.Error => _localizer["RAG:DocumentQueue:Status:Error"],
            _ => status.ToString()
        };

    /// <summary>
    /// Resolves display text for document source kind.
    /// </summary>
    public string GetSourceKindDisplay(DocumentQueueItem document)
        => string.Equals(document.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase)
            ? _localizer["KnowledgeBase:Documents:Source:Markdown"].Value
            : string.Equals(document.SourceKind, KnowledgeDocumentSourceKinds.Uploaded, StringComparison.OrdinalIgnoreCase)
                ? _localizer["KnowledgeBase:Documents:Source:Uploaded"].Value
                : _localizer["KnowledgeBase:Documents:Source:Unknown"].Value;
}
