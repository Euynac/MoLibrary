using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.MoChainTracing.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing.Implementations;

/// <summary>
/// 调用链追踪作用域，实现 IDisposable 模式
/// </summary>
public class ChainTracingScope : IDisposable
{
    private readonly IMoChainTracing _chainTracing;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    public ChainTracingScope(IMoChainTracing chainTracing, string operation, string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        _chainTracing = chainTracing;
        TraceId = _chainTracing.BeginTrace(operation, handler, extraInfo, type);
    }

    /// <summary>
    /// 调用链节点标识
    /// </summary>
    public string TraceId { get; }

    /// <summary>
    /// 记录成功结果
    /// </summary>
    /// <param name="result">结果描述</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndWithSuccess(string? result = null, object? extraInfo = null)
    {
        if (!_disposed)
        {
            _chainTracing.EndTrace(TraceId, result ?? "Success", true, null, extraInfo);
            _disposed = true;
        }
    }

    /// <summary>
    /// 记录失败结果
    /// </summary>
    /// <param name="result">结果描述</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndWithFailure(string? result = null, object? extraInfo = null)
    {
        if (!_disposed)
        {
            _chainTracing.EndTrace(TraceId, result ?? "Failed", false, null, extraInfo);
            _disposed = true;
        }
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    /// <param name="exception">异常信息</param>
    /// <param name="result">结果描述</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndWithException(Exception exception, string? result = null, object? extraInfo = null)
    {
        if (!_disposed)
        {
            _chainTracing.EndTrace(TraceId, result ?? $"Exception: {exception.GetMessageRecursively()}", false, exception, extraInfo);
            _disposed = true;
        }
    }

    /// <summary>
    /// 合并远程调用链信息
    /// </summary>
    /// <param name="remoteChainInfo">远程调用链信息</param>
    public void MergeRemoteChain(IMoResponse remoteChainInfo)
    {
        if (!_disposed)
        { 
            _chainTracing.MergeRemoteChain(TraceId, remoteChainInfo);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (!_chainTracing.ContainsTrace(TraceId))
            {
                _chainTracing.EndTrace(TraceId, "Auto-Completed", true, null);
            }
            _disposed = true;
        }
    }
}