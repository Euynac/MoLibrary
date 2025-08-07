using MoLibrary.Framework.Core.Interfaces;

namespace MoLibrary.Framework.Core.Attributes;

/// <summary>
/// 项目单元信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UnitInfoAttribute : Attribute, IUnitCachedAttribute
{
    /// <summary>
    /// 单元名称
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 相关业务组
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 单元描述
    /// </summary>
    public string? Description { get; set; }
    public UnitInfoAttribute(string name)
    {
        Name = name;
    }
}