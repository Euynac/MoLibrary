using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Tool.Results;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service for chunker management and chunking test operations.
/// </summary>
public class ChunkerManagementUIService(
    RAGService ragService,
    ILogger<ChunkerManagementUIService> logger) : IChunkerManagementUIService
{
    public async Task<Res<ChunkerManagementState>> GetManagementStateAsync()
    {
        try
        {
            var state = await ragService.GetChunkerManagementStateAsync();
            return Res.Ok(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load chunker management state.");
            return Res.Fail($"Failed to load chunker configuration: {ex.Message}");
        }
    }

    public async Task<Res<ChunkerRoutingChangePreview>> PreviewRoutingChangeAsync(
        string extension,
        string targetChunkerId)
    {
        try
        {
            var preview = await ragService.PreviewChunkerRoutingChangeAsync(extension, targetChunkerId);
            return Res.Ok(preview);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to preview chunker routing change for extension '{Extension}' to '{ChunkerId}'.",
                extension,
                targetChunkerId);
            return Res.Fail($"Failed to preview routing change: {ex.Message}");
        }
    }

    public async Task<Res<ChunkerRoutingApplyResult>> ApplyRoutingChangeAsync(
        string extension,
        string targetChunkerId)
    {
        try
        {
            var result = await ragService.ApplyChunkerRoutingChangeAsync(extension, targetChunkerId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to apply chunker routing change for extension '{Extension}' to '{ChunkerId}'.",
                extension,
                targetChunkerId);
            return Res.Fail($"Failed to apply routing change: {ex.Message}");
        }
    }

    public async Task<Res<ChunkerTestResult>> TestChunkerAsync(
        string chunkerId,
        string documentName,
        string content)
    {
        try
        {
            var result = await ragService.TestChunkerAsync(chunkerId, documentName, content);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test chunker '{ChunkerId}'.", chunkerId);
            return Res.Fail($"Failed to test chunker: {ex.Message}");
        }
    }
}
