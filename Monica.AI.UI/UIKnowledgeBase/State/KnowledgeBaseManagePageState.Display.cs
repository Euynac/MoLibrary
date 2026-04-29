using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
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
    /// Resolves one model display string by the composite model key.
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
    /// Resolves the persisted embedding model key for one knowledge base.
    /// </summary>
    public string GetKnowledgeBaseModelKey(KnowledgeBaseModel knowledgeBase)
    {
        if (!IsRagEnabled(knowledgeBase))
        {
            return string.Empty;
        }

        return EmbeddingModelOption.ToModelKey(
            knowledgeBase.EmbeddingProviderId!,
            knowledgeBase.EmbeddingModelName!);
    }

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
