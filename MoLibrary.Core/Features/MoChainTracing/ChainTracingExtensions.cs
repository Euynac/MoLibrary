using MoLibrary.Core.Features.MoChainTracing.Implementations;
using MoLibrary.Core.Features.MoChainTracing.Models;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪扩展方法
/// </summary>
public static class ChainTracingExtensions
{
    /// <summary>
    /// 开始作用域追踪
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    /// <returns>作用域追踪器</returns>
    public static ChainTracingScope BeginScope(this IMoChainTracing chainTracing,
        string operation,
        string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return new ChainTracingScope(chainTracing, operation, handler, extraInfo, type);
    }
}