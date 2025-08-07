using MoLibrary.Framework.Core.Interfaces;

namespace MoLibrary.Framework.Core.Attributes;

/// <summary>
/// 项目单元信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UnitInfoAttribute : Attribute, IUnitCachedAttribute
{
    /// <summary>
    /// 项目单元名称，将会显示在相关的UI界面上。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 相关业务组，用于绑定一个或多个需求
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// 单元描述，介绍，可使用markdown语法。
    /// </summary>
    public string? Description { get; set; }
    public UnitInfoAttribute(string name)
    {
        Name = name;
    }
}