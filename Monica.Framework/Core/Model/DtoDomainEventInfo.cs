namespace Monica.Framework.Core.Model;

/// <summary>
/// Domain event information Dto
/// </summary>
public class DtoDomainEventInfo
{
    /// <summary>
    /// Project unit information
    /// </summary>
    public DtoProjectUnit Info { get; set; } = null!;

    /// <summary>
    /// event structure information
    /// </summary>
    public object? Structure { get; set; } = null!;
}