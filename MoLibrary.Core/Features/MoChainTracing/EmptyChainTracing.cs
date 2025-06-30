namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 空调用链追踪实现，用于禁用调用链追踪功能
/// </summary>
/// <remarks>
/// 采用空对象模式，所有方法都是无操作实现，不会产生任何调用链追踪信息
/// </remarks>
public class EmptyChainTracing : IMoChainTracing
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static readonly EmptyChainTracing Instance = new();

    /// <summary>
    /// 私有构造函数，确保单例
    /// </summary>
    private EmptyChainTracing() { }

    /// <summary>
    /// 开始一个新的调用链节点（无操作）
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>空的调用链节点标识</returns>
    public string BeginTrace(string handler, string operation, object? extraInfo = null)
    {
        return string.Empty;
    }

    /// <summary>
    /// 完成一个调用链节点（无操作）
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果描述</param>
    /// <param name="success">是否成功</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, object? extraInfo = null)
    {
        // 无操作
    }

    /// <summary>
    /// 记录异常信息（无操作）
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordException(string traceId, Exception exception, object? extraInfo = null)
    {
        // 无操作
    }

    /// <summary>
    /// 记录简单的调用信息（无操作）
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="result">调用结果</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordTrace(string handler, string operation, bool success = true, string? result = null, 
        TimeSpan? duration = null, object? extraInfo = null)
    {
        // 无操作
    }

    /// <summary>
    /// 获取当前的调用链信息（始终返回 null）
    /// </summary>
    /// <returns>始终返回 null</returns>
    public MoChainContext? GetCurrentChain()
    {
        return null;
    }

    /// <summary>
    /// 清空当前的调用链信息（无操作）
    /// </summary>
    public void ClearChain()
    {
        // 无操作
    }

    /// <summary>
    /// 使用指定的调用链上下文执行操作（直接执行操作，忽略调用链上下文）
    /// </summary>
    /// <param name="context">调用链上下文</param>
    /// <param name="action">要执行的操作</param>
    public async Task ExecuteWithChainAsync(MoChainContext? context, Func<Task> action)
    {
        await action();
    }

    /// <summary>
    /// 使用指定的调用链上下文执行操作（直接执行操作，忽略调用链上下文）
    /// </summary>
    /// <param name="context">调用链上下文</param>
    /// <param name="func">要执行的操作</param>
    public async Task<T> ExecuteWithChainAsync<T>(MoChainContext? context, Func<Task<T>> func)
    {
        return await func();
    }
}