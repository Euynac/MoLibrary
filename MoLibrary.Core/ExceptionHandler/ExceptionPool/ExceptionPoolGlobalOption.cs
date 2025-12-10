namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池全局配置选项
/// 为所有异常池提供默认配置值
/// </summary>
public class ExceptionPoolGlobalOption
{
    /// <summary>
    /// 异常池默认最大容量
    /// 当实例配置未指定时使用此值
    /// </summary>
    public int DefaultMaxSize { get; set; } = 10;

    /// <summary>
    /// 是否启用事件触发
    /// 当实例配置未指定时使用此值
    /// </summary>
    public bool EnableEventTrigger { get; set; } = true;
}
