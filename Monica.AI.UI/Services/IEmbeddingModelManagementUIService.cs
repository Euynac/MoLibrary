using Monica.AI.UI.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service contract for embedding model discovery and knowledge-base binding.
/// </summary>
public interface IEmbeddingModelManagementUIService
{
    Task<Res<IReadOnlyList<EmbeddingModelOption>>> GetEmbeddingModelsAsync();

    Task<Res<EmbeddingModelOption?>> GetKnowledgeBaseEmbeddingModelAsync(string kbId);

    Task<Res> SetKnowledgeBaseEmbeddingModelAsync(
        string kbId,
        string providerId,
        string modelName,
        bool clearIndex = true);
}
