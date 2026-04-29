using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Check whether one knowledge base already has an embedding binding.
    /// </summary>
    public static bool HasEmbeddingBinding(KnowledgeBaseModel knowledgeBase)
        => !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
           && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName);

    /// <summary>
    /// Resolve the display label of one provider.
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
    /// Resolve one knowledge-base embedding display string.
    /// </summary>
    public string GetKnowledgeBaseModelDisplay(KnowledgeBaseModel knowledgeBase)
    {
        if (!HasEmbeddingBinding(knowledgeBase))
        {
            return string.Empty;
        }

        return $"{knowledgeBase.EmbeddingModelName} ({GetProviderDisplayLabel(knowledgeBase.EmbeddingProviderId, knowledgeBase.EmbeddingModelName)})";
    }

    /// <summary>
    /// Build the missing-vector warning copy for the current selection.
    /// </summary>
    public string GetMissingVectorWarningMessage()
    {
        if (SelectedKnowledgeBaseVectorValidation is null)
        {
            return string.Empty;
        }

        return _localizer["RAG:VectorValidation:MissingVectors:Message",
            SelectedKnowledgeBaseVectorValidation.IndexedDocumentCount,
            SelectedKnowledgeBaseVectorValidation.IndexedChunkCount];
    }
}
