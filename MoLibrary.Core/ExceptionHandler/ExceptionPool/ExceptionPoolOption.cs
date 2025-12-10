namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池实例配置选项
/// 提供实例级别的配置，优先级高于全局配置
/// </summary>
public class ExceptionPoolOption
{
    /// <summary>
    /// 异常池标识符
    /// </summary>
    public string PoolId { get; set; } = string.Empty;

    /// <summary>
    /// 异常池最大容量
    /// 如果为 null，则使用全局配置的默认值
    /// </summary>
    public int? MaxSize { get; set; }

    /// <summary>
    /// 是否启用事件触发
    /// 如果为 null，则使用全局配置的默认值
    /// </summary>
    public bool? EnableEventTrigger { get; set; }
}
