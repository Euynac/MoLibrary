using MoLibrary.Core.Features.MoChainTracing.Models;

namespace MoLibrary.Core.Features.MoChainTracing.Implementations;

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
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    /// <returns>空的调用链节点标识</returns>
    public string BeginTrace(string operation, string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return string.Empty;
    }

    /// <summary>
    /// 完成一个调用链节点（无操作）
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果描述</param>
    /// <param name="success">是否成功</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, Exception? exception = null,
        object? extraInfo = null)
    {
        // 无操作
    }


    /// <summary>
    /// 记录简单的调用信息（无操作）
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="result">调用结果</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    public void RecordTrace(string operation, string? handler, bool success = true, string? result = null,
        TimeSpan? duration = null, object? extraInfo = null, EChainTracingType type = EChainTracingType.Unknown)
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
    /// 合并远程调用链信息（无操作）
    /// </summary>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="remoteChainInfo">远程调用链信息</param>
    /// <returns>始终返回 false</returns>
    public bool MergeRemoteChain(string traceId, object? remoteChainInfo)
    {
        // 无操作
        return false;
    }

    public bool ContainsTrace(string traceId)
    {
        return false;
    }
}