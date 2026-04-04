namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Status snapshot of a DataChannel for the UI layer.
/// </summary>
public class DataChannelStatusInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Configured middleware instances.
    /// </summary>
    public List<PipelineComponentInfo> Middlewares { get; set; } = new();
    
    /// <summary>
    /// Representation of the pipeline's inner endpoint.
    /// </summary>
    public PipelineComponentInfo InnerEndpoint { get; set; } = null!;
    
    /// <summary>
    /// Representation of the pipeline's outer endpoint.
    /// </summary>
    public PipelineComponentInfo OuterEndpoint { get; set; } = null!;
    
    /// <summary>
    /// Flag indicating whether the channel is currently unavailable.
    /// </summary>
    public bool IsNotAvailable { get; set; }
    
    /// <summary>
    /// Indicates whether the channel has finished initialization.
    /// </summary>
    public bool IsInitialized { get; set; }
    
    /// <summary>
    /// Indicates whether the channel is currently initializing.
    /// </summary>
    public bool IsInitializing { get; set; }
    
    /// <summary>
    /// Indicates whether the channel has outstanding exceptions.
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// Number of active exceptions in the channel.
    /// </summary>
    public int ExceptionCount { get; set; }
    
    /// <summary>
    /// Cumulative exception count for the channel.
    /// </summary>
    public int TotalExceptionCount { get; set; }
}
