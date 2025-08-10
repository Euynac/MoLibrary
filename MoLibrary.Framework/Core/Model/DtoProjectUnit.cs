using System.Text.Json.Serialization;
using MoLibrary.Framework.Core.Interfaces;

namespace MoLibrary.Framework.Core.Model;

public class DtoProjectUnit
{
    /// <summary>
    /// 项目单元键值，也即项目单元FullName名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// 项目单元描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 项目单元作者
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 项目单元分组信息
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// 项目单元类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }

    /// <summary>
    /// 所依赖的项目单元
    /// </summary>
    public List<DtoProjectUnitDependency> DependencyUnits { get; set; } = [];

    /// <summary>
    /// 项目单元特性
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; set; } = [];
    
    /// <summary>
    /// 告警信息列表
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; set; } = [];

    /// <summary>
    /// 项目单元方法列表
    /// </summary>
    [JsonIgnore]
    public List<ProjectUnitMethod> Methods { get; set; } = [];
    
    /// <summary>
    /// 被依赖的数量（在数据传输时计算）
    /// </summary>
    public int DependedByCount { get; set; } = 0;
}