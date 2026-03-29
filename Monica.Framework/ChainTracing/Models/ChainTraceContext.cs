using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Framework.ChainTracing.Models;

/// <summary>
/// Stores the state for a single call chain.
/// </summary>
public class ChainTraceContext
{
    /// <summary>
    /// Extra-info key used to store serialized chain data.
    /// </summary>
    public const string CHAIN_KEY = "chain";

    /// <summary>
    /// Root node of the chain.
    /// </summary>
    public ChainTraceNode? Root { get; set; }

    /// <summary>
    /// Stack of currently active nodes.
    /// </summary>
    [JsonIgnore]
    public Stack<ChainTraceNode> ActiveNodes { get; set; } = new();

    /// <summary>
    /// Nodes that became detached because the trace closed out of order.
    /// </summary>
    public List<ChainTraceNode>? IsolatedNodes { get; set; }

    /// <summary>
    /// Lookup table for trace nodes by identifier.
    /// </summary>
    [JsonIgnore]
    public ConcurrentDictionary<string, ChainTraceNode> NodeMap { get; set; } = new();

    /// <summary>
    /// Time when the chain started.
    /// </summary>
    [JsonIgnore]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time when the chain completed.
    /// </summary>
    [JsonIgnore]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Additional metadata for the chain.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? OtherInfo { get; set; }

    /// <summary>
    /// Returns whether nodes of the specified type should stay on the active stack.
    /// High-volume operations without child calls can skip stacking to avoid mismatched closure and JSON cycles.
    /// </summary>
    /// <param name="type">The trace node type.</param>
    /// <returns><see langword="true" /> when nodes of this type can participate in the active stack.</returns>
    public static bool CanHaveChildOperations(EChainTracingType type)
    {
        return type != EChainTracingType.Database;
    }

    /// <summary>
    /// Adds a new node to the current chain.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public void AddNode(ChainTraceNode node)
    {
        if (Root == null)
        {
            Root = node;
            node.Depth = 1;
        }
        else if (ActiveNodes.Count > 0)
        {
            var parent = ActiveNodes.Peek();
            parent.Children ??= [];
            parent.Children.Add(node);
            node.SetParent(parent);
        }

        if (CanHaveChildOperations(node.Type))
        {
            ActiveNodes.Push(node);
        }

        NodeMap[node.TraceId] = node;
    }

    /// <summary>
    /// Completes a node in the current chain.
    /// </summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="result">A description of the result.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="exception">The captured exception, if any.</param>
    /// <param name="extraInfo">Optional completion metadata.</param>
    public void CompleteNode(string traceId, string? result = null, bool success = true, Exception? exception = null, object? extraInfo = null)
    {
        if (!NodeMap.TryGetValue(traceId, out var node))
        {
            return;
        }

        node.EndTime = DateTime.UtcNow;
        node.Result = result;
        node.Exception = exception;
        node.EndExtraInfo = extraInfo;

        if (exception != null || !success)
        {
            node.IsFailed = true;
        }

        if (ActiveNodes.Count <= 0)
        {
            return;
        }

        while (ActiveNodes.Count > 0)
        {
            var topNode = ActiveNodes.Pop();
            if (topNode.TraceId == traceId)
            {
                break;
            }

            IsolatedNodes ??= [];
            IsolatedNodes.Add(topNode);
        }
    }

    /// <summary>
    /// Marks the chain as completed.
    /// </summary>
    public void MarkComplete()
    {
        EndTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a deep clone of the chain.
    /// </summary>
    /// <returns>A deep clone of the current chain.</returns>
    public ChainTraceContext Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<ChainTraceContext>(json) ?? new ChainTraceContext();
    }

    /// <summary>
    /// Merges a remote chain beneath a local trace node.
    /// </summary>
    /// <param name="traceId">The local trace identifier.</param>
    /// <param name="remoteChainNode">The remote chain root node.</param>
    /// <param name="maxChainDepth">The maximum allowed chain depth.</param>
    /// <returns><see langword="true" /> when the merge succeeds; otherwise, <see langword="false" />.</returns>
    public bool MergeRemoteChain(string traceId, ChainTraceNode? remoteChainNode, int maxChainDepth)
    {
        if (remoteChainNode == null || !NodeMap.TryGetValue(traceId, out var currentNode))
        {
            return false;
        }

        currentNode.Children ??= [];
        currentNode.Children.Add(remoteChainNode);
        remoteChainNode.SetParent(currentNode);
        remoteChainNode.RecalculateDepthAndTrim(remoteChainNode.Depth, maxChainDepth);
        return true;
    }
}
