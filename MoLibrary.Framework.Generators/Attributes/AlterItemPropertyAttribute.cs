using System;

namespace MoLibrary.Framework.Generators.Attributes;

/// <summary>
/// 用于配置AlterItemData生成时的属性行为
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AlterItemPropertyAttribute : Attribute
{
    /// <summary>
    /// 是否忽略该属性，不在AlterItemData中生成
    /// </summary>
    public bool Ignore { get; set; } = false;
    
    /// <summary>
    /// 属性的标题，用于后续生成Format方法
    /// </summary>
    public string? Title { get; set; }
}