namespace Monica.Core.Features.MoMapper;

/// <summary>
/// Summarizes the object mappings currently registered in the application.
/// </summary>
public class ObjectMapperStatusResponse
{
    /// <summary>
    /// Total number of discovered mappings.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Detailed information for each discovered mapping.
    /// </summary>
    public IReadOnlyList<ObjectMapperInfo> Mappings { get; set; } = Array.Empty<ObjectMapperInfo>();
}
