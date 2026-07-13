using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.RAG.Facades;

/// <summary>
/// Host-facing facade for chunker management and chunking test operations.
/// </summary>
public sealed class ChunkerFacade
{
    private readonly RAGChunkingService chunkingService;
    private readonly ILogger<ChunkerFacade> logger;

    internal ChunkerFacade(
        RAGChunkingService chunkingService,
        ILogger<ChunkerFacade> logger)
    {
        this.chunkingService = chunkingService;
        this.logger = logger;
    }

    public async Task<Res<ChunkerManagementState>> GetManagementStateAsync()
    {
        try
        {
            var state = await chunkingService.GetManagementStateAsync();
            return Res.Ok(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load chunker management state.");
            return Res.Fail($"Failed to load chunker configuration: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<ChunkerRoutingChangePreview>> PreviewRoutingChangeAsync(
        string extension,
        string targetChunkerId)
    {
        try
        {
            var preview = await chunkingService.PreviewRoutingChangeAsync(extension, targetChunkerId);
            return Res.Ok(preview);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to preview chunker routing change for extension '{Extension}' to '{ChunkerId}'.",
                extension,
                targetChunkerId);
            return Res.Fail($"Failed to preview routing change: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<ChunkerRoutingApplyResult>> ApplyRoutingChangeAsync(
        string extension,
        string targetChunkerId)
    {
        try
        {
            var result = await chunkingService.ApplyRoutingChangeAsync(extension, targetChunkerId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to apply chunker routing change for extension '{Extension}' to '{ChunkerId}'.",
                extension,
                targetChunkerId);
            return Res.Fail($"Failed to apply routing change: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<ChunkerTestResult>> TestChunkerAsync(
        string chunkerId,
        string documentName,
        string content)
    {
        try
        {
            var result = await chunkingService.TestAsync(chunkerId, documentName, content);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test chunker '{ChunkerId}'.", chunkerId);
            return Res.Fail($"Failed to test chunker: {ex.GetMessageRecursively()}");
        }
    }
}
