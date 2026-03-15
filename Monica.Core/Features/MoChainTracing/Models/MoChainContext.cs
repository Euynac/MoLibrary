using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.Features.MoChainTracing.Models;

/// <summary>
/// Stores the state for a single call chain.
/// </summary>
public class MoChainContext
{
    /// <summary>
    /// Extra-info key used to store serialized chain data.
    /// </summary>
    public const string CHAIN_KEY = "chain";
    /// <summary>
    /// Root node of the chain.
    /// </summary>
    public MoChainNode? Root { get; set; }

    /// <summary>
    /// Stack of currently active nodes.
    /// </summary>
    [JsonIgnore]
    public Stack<MoChainNode> ActiveNodes { get; set; } = new();

    /// <summary>
    /// Nodes that became detached because the trace closed out of order.
    /// </summary>
    public List<MoChainNode>? IsolatedNodes { get; set; }

    /// <summary>
    /// Lookup table for trace nodes by identifier.
    /// </summary>
    [JsonIgnore]
    public ConcurrentDictionary<string, MoChainNode> NodeMap { get; set; } = new();


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
    public static bool CanHasChildrenOperation(EChainTracingType type)
    {
        return type != EChainTracingType.Database;
    }

    /// <summary>
    /// Adds a new node to the current chain.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="maxNodeCount">The configured maximum node count.</param>
    public void AddNode(MoChainNode node, int maxNodeCount)
    {
        if (Root == null)
        {
            Root = node;
            node.Deepth = 1;
        }
        else if (ActiveNodes.Count > 0)
        {
            var parent = ActiveNodes.Peek();
            parent.Children ??= [];
            parent.Children.Add(node);
            node.SetParent(parent);
        }

        if (CanHasChildrenOperation(node.Type) || node.Deepth <= maxNodeCount)
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
        if (NodeMap.TryGetValue(traceId, out var node))
        {
            node.EndTime = DateTime.UtcNow;
            node.Result = result;
            node.Exception = exception;
            node.EndExtraInfo = extraInfo;

            // An exception always marks the node as failed.
            if (exception != null || !success)
            {
                node.IsFailed = true;
            }

            // Pop active nodes until the completed trace is found; earlier mismatches become isolated nodes.
            if (ActiveNodes.Count > 0)
            {
                while (ActiveNodes.Pop() is { } topNode && topNode.TraceId != traceId)
                {
                    IsolatedNodes ??= [];
                    IsolatedNodes.Add(topNode);
                }
            }
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
    public MoChainContext Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<MoChainContext>(json) ?? new MoChainContext();
    }

    /// <summary>
    /// Merges a remote chain beneath a local trace node.
    /// </summary>
    /// <param name="traceId">The local trace identifier.</param>
    /// <param name="remoteChainNode">The remote chain root node.</param>
    /// <param name="maxChainDepth">The maximum allowed chain depth.</param>
    /// <returns><see langword="true" /> when the merge succeeds; otherwise, <see langword="false" />.</returns>
    public bool MergeRemoteChain(string traceId, MoChainNode? remoteChainNode, int maxChainDepth)
    {
        if (remoteChainNode == null) return false;
        if (!NodeMap.TryGetValue(traceId, out var currentNode))
        {
            return false;
        }

        // Attach the remote chain beneath the current node.
        currentNode.Children ??= [];
        currentNode.Children.Add(remoteChainNode);
        remoteChainNode.SetParent(currentNode);
        remoteChainNode.ReCalculateDepthAndClean(remoteChainNode.Deepth, maxChainDepth);
        return true;
    }
}
