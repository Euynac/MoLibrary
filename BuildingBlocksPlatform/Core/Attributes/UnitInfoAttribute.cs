using BuildingBlocksPlatform.Core.Interfaces;

namespace BuildingBlocksPlatform.Core.Attributes;

/// <summary>
/// 项目单元信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UnitInfoAttribute : Attribute, IUnitCachedAttribute
{
    /// <summary>
    /// 业务名称
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 业务组
    /// </summary>
    public string? Group { get; set; }
    public UnitInfoAttribute(string name)
    {
        Name = name;
    }
}