using Monica.AI.RAG.Models;
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

        var model = AvailableModels.FirstOrDefault(option =>
            string.Equals(option.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(modelName)
                || string.Equals(option.ModelName, modelName, StringComparison.OrdinalIgnoreCase)));

        if (model is not null)
        {
            return model.ProviderDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var modelCandidates = AvailableModels
                .Where(option => string.Equals(option.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
                .Select(option => option.ProviderDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (modelCandidates.Count == 1)
            {
                return modelCandidates[0];
            }
        }

        var provider = _providerFactory.GetProvider(providerId);
        if (provider is not null)
        {
            return provider.DisplayName;
        }

        return providerId.Trim();
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
    /// Resolve one model display string by the composite model key.
    /// </summary>
    public string GetModelDisplayByKey(string modelKey)
    {
        var model = AvailableModels.FirstOrDefault(option =>
            string.Equals(option.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase));

        if (model is not null)
        {
            return $"{model.ModelName} ({model.ProviderDisplayName})";
        }

        if (!EmbeddingModelOption.TryParseModelKey(modelKey, out var providerId, out var modelName))
        {
            return modelKey;
        }

        return $"{modelName} ({GetProviderDisplayLabel(providerId, modelName)})";
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

    /// <summary>
    /// Resolve the persisted embedding model key for one knowledge base.
    /// </summary>
    public string GetKnowledgeBaseModelKey(KnowledgeBaseModel knowledgeBase)
    {
        if (!HasEmbeddingBinding(knowledgeBase))
        {
            return string.Empty;
        }

        return EmbeddingModelOption.ToModelKey(
            knowledgeBase.EmbeddingProviderId!,
            knowledgeBase.EmbeddingModelName!);
    }
}
