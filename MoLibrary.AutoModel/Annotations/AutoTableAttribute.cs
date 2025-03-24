using MoLibrary.AutoModel.Configurations;

namespace MoLibrary.AutoModel.Annotations;
[AttributeUsage(AttributeTargets.Class)]
public class AutoTableAttribute : Attribute
{
    /// <summary>
    /// 主动模式（仅打了AutoField标签的字段才会启用自动模型功能）为false时则默认为被动模式。为null为使用全局模式设置
    /// </summary>
    public bool? ActiveMode { get; set; }
    /// <summary>
    /// 表名
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// <inheritdoc cref="AutoModelOptions.EnableIgnorePrefix"/>，若为null运用上层设置
    /// </summary>
    public bool? EnableIgnorePrefix { get; set; }
}