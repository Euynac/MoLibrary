using MoLibrary.AutoModel.Modules;

namespace MoLibrary.AutoModel.Annotations;
[AttributeUsage(AttributeTargets.Property)]
public class AutoFieldAttribute : Attribute
{
    /// <summary>
    /// 字段额外激活名(默认包含反射名)
    /// </summary>
    public List<string>? ActivateNames { get; set; }

    /// <summary>
    /// 字段显示名
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// <inheritdoc cref="ModuleAutoModelOption.EnableTitleAsActivateName"/>，若为null运用上层设置
    /// </summary>
    [Obsolete("暂未实现")]
    public bool? TitleAsActivateName { get; set; }

    /// <summary>
    /// 全字段模糊查询时是否忽略该字段
    /// </summary>
    public bool IgnoreFuzzColumn { get; set; }

    /// <summary>
    /// 字段过滤时必填
    /// </summary>
    [Obsolete("暂未实现")]
    public bool IsRequired { get; set; }

    /// <summary>
    /// 忽略该字段，不进行自动模型字段的构建
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// <inheritdoc cref="ModuleAutoModelOption.EnableIgnorePrefix"/>，若为null运用上层设置
    /// </summary>
    public bool? EnableIgnorePrefix { get; set; }
}