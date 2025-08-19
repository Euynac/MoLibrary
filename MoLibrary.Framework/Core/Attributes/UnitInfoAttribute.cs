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

    /// <summary>
    /// Markdown文档描述，链接或路径，通常用于提供更详细的文档说明。
    /// </summary>
    /// <remarks>自定义语法：@用户管理.md#权限控制 可以生成相应配置的文档服务的超链接地址（暂未实现）</remarks>
    public string? MarkdownDocs { get; set; }
}