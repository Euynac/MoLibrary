using MoLibrary.Framework.Core.Interfaces;

namespace MoLibrary.Framework.Core.Attributes;

/// <summary>
/// 项目单元信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UnitInfoAttribute(string name) : Attribute, IUnitCachedAttribute
{
    /// <summary>
    /// 项目单元名称，将会显示在相关的UI界面上。
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// 相关业务组，用于绑定一个或多个需求
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// 功能介绍文档，使用markdown语法。
    /// </summary>
    public string? MarkdownDescription { get; set; }

    /// <summary>
    /// 项目单元描述，通常用于简要说明该单元的功能或用途。如该属性为空，则读取XML注释中的summary内容。
    /// </summary>
    public string? Description { get; set; }
}