using MoLibrary.Core.Features.MoChainTracing.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪接口，用于记录应用层接口的调用链信息
/// </summary>
public interface IMoChainTracing
{
    /// <summary>
    /// 开始一个新的调用链节点
    /// </summary>
    /// <param name="operation">操作名称（如方法名、操作描述等）</param>
    /// <param name="handler">处理者名称（如服务名、类名等）</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    /// <returns>调用链节点标识，用于后续完成调用</returns>
    string BeginTrace(string operation, string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown);

    /// <summary>
    /// 完成一个调用链节点
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果描述</param>
    /// <param name="success">是否成功</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    void EndTrace(string traceId, string? result = null, bool success = true, Exception? exception = null,
        object? extraInfo = null);

    /// <summary>
    /// 检查是否包含指定的调用链节点
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <returns>是否包含</returns>
    bool ContainsTrace(string traceId);

    /// <summary>
    /// 记录简单的调用信息（一次性记录，适用于简单调用）
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="result">调用结果</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    void RecordTrace(string operation, string? handler, bool success = true, string? result = null,
        TimeSpan? duration = null, object? extraInfo = null, EChainTracingType type = EChainTracingType.Unknown);

    /// <summary>
    /// 获取当前的调用链信息
    /// </summary>
    /// <returns>调用链信息，如果当前没有调用链则返回null</returns>
    MoChainContext? GetCurrentChain();

    /// <summary>
    /// 合并远程调用链信息
    /// </summary>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="remoteRes"></param>
    /// <returns>是否成功合并</returns>
    void MergeRemoteChain(string traceId, IMoResponse remoteRes);

    /// <summary>
    /// 一般用于 AsyncLocal 最外层初次赋值。但最好在最外层开启一个Chain。
    /// </summary>
    void Init();
} 