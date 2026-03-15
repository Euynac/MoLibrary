using Monica.Core.Features.MoChainTracing.Implementations;
using Monica.Core.Features.MoChainTracing.Models;

namespace Monica.Core.Features.MoChainTracing;

/// <summary>
/// Extension methods for chain tracing.
/// </summary>
public static class ChainTracingExtensions
{
    /// <summary>
    /// Begins a scoped trace entry.
    /// </summary>
    /// <param name="chainTracing">The chain tracing service.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="handler">The handler name.</param>
    /// <param name="extraInfo">Optional extra metadata.</param>
    /// <param name="type">The traced operation type.</param>
    /// <returns>A disposable tracing scope.</returns>
    public static ChainTracingScope BeginScope(this IMoChainTracing chainTracing,
        string operation,
        string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return new ChainTracingScope(chainTracing, operation, handler, extraInfo, type);
    }
}
