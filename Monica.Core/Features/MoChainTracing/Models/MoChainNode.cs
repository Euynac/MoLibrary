using System.Text.Json.Serialization;

namespace Monica.Core.Features.MoChainTracing.Models;

/// <summary>
/// Represents a single node in a call chain.
/// </summary>
public class MoChainNode
{
    /// <summary>
    /// Depth of the node within the chain.
    /// </summary>
    public int Deepth { get; set; }
    
    /// <summary>
    /// Sets the parent node and updates the depth.
    /// </summary>
    /// <param name="parent">The parent node.</param>
    public void SetParent(MoChainNode parent)
    {
        Deepth = parent.Deepth + 1;
        Parent = parent;
    }
    #region Merge support

    private string[]? _exceptionMessage;
    private string? _duration;
    private EChainTracingType _type;
    /// <summary>
    /// Recalculates descendant depths and prunes children beyond the depth limit.
    /// </summary>
    public void ReCalculateDepthAndClean(int currentDeepth, int maxChainDepth)
    {
        Deepth = currentDeepth;

        if (Children == null || Children.Count < 0)
        {
            return;
        }


        if (currentDeepth > maxChainDepth)
        {
            Children?.Clear();
            return;
        }

        foreach (var child in Children)
        {
            child.ReCalculateDepthAndClean(currentDeepth+1, maxChainDepth);
        }
    }
    #endregion

    /// <summary>
    /// Trace node category.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EChainTracingType Type
    {
        get => _type;
        set
        {
            _type = value;
            SetRemoteAttr(_type);
        }
    }

    /// <summary>
    /// Unique identifier of the trace node.
    /// </summary>
    [JsonIgnore]
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Handler name, such as a service or class name.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Handler { get; set; }

    /// <summary>
    /// Operation name, such as a method or action description.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Time when the node started.
    /// </summary>
    [JsonIgnore]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time when the node completed.
    /// </summary>
    [JsonIgnore]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of the node formatted in milliseconds.
    /// </summary>
    public string? Duration
    {
        get => _duration ?? (EndTime?.Subtract(StartTime).TotalMilliseconds is { } milliseconds ? $"{milliseconds}ms" : null);
        set => _duration = value;
    }

    /// <summary>
    /// Description of the result.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    /// <summary>
    /// Indicates whether the call failed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsFailed { get; set; }

    /// <summary>
    /// Indicates whether this node represents a remote call.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsRemoteCall { get; set; }

    /// <summary>
    /// Captured exception.
    /// </summary>
    [JsonIgnore]
    public Exception? Exception { get; set; }

    /// <summary>
    /// Serialized representation of the exception.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ExceptionMessage
    {
        get => _exceptionMessage ?? Exception?.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        set => _exceptionMessage = value;
    }

    /// <summary>
    /// Extra metadata captured when the node starts.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StartExtraInfo { get; set; }

    /// <summary>
    /// Extra metadata captured when the node ends.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? EndExtraInfo { get; set; }

    /// <summary>
    /// Child trace nodes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MoChainNode>? Children { get; set; }

    /// <summary>
    /// Parent trace node.
    /// </summary>
    [JsonIgnore]
    public MoChainNode? Parent { get; private set; }

    /// <summary>
    /// Optional notes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; set; }


    public void SetRemoteAttr(EChainTracingType type)
    {
        IsRemoteCall = type == EChainTracingType.RemoteService;
    }


    public override string ToString()
    {
        return $"[{Type}]{Handler}-{Operation}";
    }
}
