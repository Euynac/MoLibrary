using Microsoft.Extensions.Logging;
using Monica.Core.Results;

namespace Monica.Core.Features.MoMapper;

/// <summary>
/// Provides object mapping inspection data for debug endpoints and UI pages.
/// </summary>
public class ObjectMappingStatusService(ILogger<ObjectMappingStatusService> logger)
{
    /// <summary>
    /// Gets the current object mapping status snapshot.
    /// </summary>
    /// <returns>The current object mapping status.</returns>
    public Task<Res<ObjectMapperStatusResponse>> GetStatusAsync()
    {
        try
        {
            var mappings = MapsterMappingInspector.GetMappings();
            logger.LogDebug("Retrieved object mapping status with {Count} mappings.", mappings.Count);

            return Task.FromResult(Res.Ok(new ObjectMapperStatusResponse
            {
                Count = mappings.Count,
                Mappings = mappings
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve object mapping status.");
            return Task.FromResult<Res<ObjectMapperStatusResponse>>($"Failed to retrieve object mapping status: {ex.Message}");
        }
    }
}
