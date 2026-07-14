using System.Text.Json.Serialization;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Identifies a project unit referenced by another unit's dependency list.
/// </summary>
public class DtoProjectUnitDependency
{
    /// <summary>
    /// Project unit key value, that is, project unit FullName name
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Project unit display name
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Project unit type
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }
}
