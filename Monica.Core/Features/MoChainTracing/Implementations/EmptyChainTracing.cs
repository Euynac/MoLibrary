using Monica.Core.Features.MoChainTracing.Models;
using Monica.Tool.Results;

namespace Monica.Core.Features.MoChainTracing.Implementations;

/// <summary>
/// No-op chain tracing implementation used when tracing is disabled.
/// </summary>
/// <remarks>
/// Uses the Null Object pattern. All methods are safe no-ops and do not produce trace data.
/// </remarks>
public class EmptyChainTracing : IMoChainTracing
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly EmptyChainTracing Instance = new();

    /// <summary>
    /// Prevents external construction.
    /// </summary>
    private EmptyChainTracing() { }

    /// <summary>
    /// Starts a new trace node.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="handler">The handler name.</param>
    /// <param name="extraInfo">Optional extra metadata.</param>
    /// <param name="type">The traced operation type.</param>
    /// <returns>An empty trace identifier.</returns>
    public string BeginTrace(string operation, string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return string.Empty;
    }

    /// <summary>
    /// Completes a trace node.
    /// </summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="result">A description of the result.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="exception">The captured exception, if any.</param>
    /// <param name="extraInfo">Optional completion metadata.</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, Exception? exception = null,
        object? extraInfo = null)
    {
    }


    /// <summary>
    /// Records a one-shot trace entry.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="handler">The handler name.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="result">A description of the result.</param>
    /// <param name="duration">The known execution duration.</param>
    /// <param name="extraInfo">Optional extra metadata.</param>
    /// <param name="type">The traced operation type.</param>
    public void RecordTrace(string operation, string? handler, bool success = true, string? result = null,
        TimeSpan? duration = null, object? extraInfo = null, EChainTracingType type = EChainTracingType.Unknown)
    {
    }

    /// <summary>
    /// Gets the current chain.
    /// </summary>
    /// <returns>Always <see langword="null" /> because tracing is disabled.</returns>
    public MoChainContext? GetCurrentChain()
    {
        return null;
    }

    /// <summary>
    /// Merges chain data returned from a remote call.
    /// </summary>
    /// <param name="traceId">The local trace identifier.</param>
    /// <param name="remoteRes">The remote response carrying chain metadata.</param>
    public void MergeRemoteChain(string traceId, IResultEnvelope remoteRes)
    {
    }

    public void Init()
    {
    }

    public bool ContainsTrace(string traceId)
    {
        return false;
    }
}
