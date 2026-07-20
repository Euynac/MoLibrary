using Monica.Core.ObjectMapping.Models;
using Monica.Core.ObjectMapping.Providers.Mapster;

namespace Monica.Core.ObjectMapping.Services;

/// <summary>
/// Provides object mapping inspection data for debug endpoints and UI pages.
/// </summary>
internal sealed class ObjectMappingStatusService(
    MapsterMappingInspector mappingInspector)
{
    public ObjectMapperStatusResponse GetStatus()
    {
        var mappings = mappingInspector.GetMappings();
        return new ObjectMapperStatusResponse
        {
            Count = mappings.Count,
            Mappings = mappings
        };
    }
}
