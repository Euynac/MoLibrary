using Monica.AI.RAG.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service contract for chunker management and chunking tests.
/// </summary>
public interface IChunkerManagementUIService
{
    Task<Res<ChunkerManagementState>> GetManagementStateAsync();

    Task<Res<ChunkerRoutingChangePreview>> PreviewRoutingChangeAsync(string extension, string targetChunkerId);

    Task<Res<ChunkerRoutingApplyResult>> ApplyRoutingChangeAsync(string extension, string targetChunkerId);

    Task<Res<ChunkerTestResult>> TestChunkerAsync(string chunkerId, string documentName, string content);
}
