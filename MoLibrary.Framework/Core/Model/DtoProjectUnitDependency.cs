using System.Text.Json.Serialization;

namespace MoLibrary.Framework.Core.Model;

public class DtoProjectUnitDependency
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
    /// 项目单元类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }
}