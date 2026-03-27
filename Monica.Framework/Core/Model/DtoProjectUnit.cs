using System.Text.Json.Serialization;
using Monica.Framework.Core.Interfaces;

namespace Monica.Framework.Core.Model;

public class DtoProjectUnit
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
    /// Project unit description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Project unit author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Project unit grouping information
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// Project unit type
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }

    /// <summary>
    /// The project unit it depends on
    /// </summary>
    public List<DtoProjectUnitDependency> DependencyUnits { get; set; } = [];

    /// <summary>
    /// Project unit properties
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; set; } = [];
    
    /// <summary>
    /// Alarm information list
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; set; } = [];

    /// <summary>
    /// Project unit method list
    /// </summary>
    [JsonIgnore]
    public List<ProjectUnitMethod> Methods { get; set; } = [];
    
    /// <summary>
    /// The number of dependencies (calculated during data transfer)
    /// </summary>
    public int DependedByCount { get; set; } = 0;
}