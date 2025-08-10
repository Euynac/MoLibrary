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
    /// 项目单元作者，通常用于标识该单元的创建者或维护者或责任人。
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 相关业务组、或需求ID、模块ID等，用于UI界面相关项目单元。
    /// </summary>
    public string[]? Group { get; set; }

    /// <summary>
    /// 项目单元描述，通常用于简要说明该单元的功能或用途。如该属性为空，则读取XML注释中的summary内容。
    /// </summary>
    public string? Description { get; set; }
}