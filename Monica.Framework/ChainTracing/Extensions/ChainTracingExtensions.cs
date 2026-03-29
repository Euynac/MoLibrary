using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Models;

namespace Monica.Framework.ChainTracing.Extensions;

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
    public static ChainTracingScope BeginScope(this IChainTracing chainTracing,
        string operation,
        string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return new ChainTracingScope(chainTracing, operation, handler, extraInfo, type);
    }
}
