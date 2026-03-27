namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Exception information for a single DataChannel.
/// </summary>
public class ChannelExceptionInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;
    
    /// <summary>
    /// Pipeline ID
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of exceptions currently held in the channel.
    /// </summary>
    public long CurrentExceptions { get; set; }

    /// <summary>
    /// Total exceptions recorded for the channel.
    /// </summary>
    public long TotalExceptions { get; set; }
    
    /// <summary>
    /// Maximum history depth for the exception pool.
    /// </summary>
    public int MaxPoolSize { get; set; }
    
    /// <summary>
    /// Indicates whether the channel currently holds exceptions.
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// Details of each recorded exception.
    /// </summary>
    public List<ExceptionDetailInfo> Exceptions { get; set; } = new();
}

/// <summary>
/// Details about a single exception.
/// </summary>
public class ExceptionDetailInfo
{
    /// <summary>
    /// Timestamp when the exception occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// CLR type of the component that raised the exception.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the source component for the exception.
    /// </summary>
    public string SourceDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// CLR exception type name.
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Exception message text.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Stack trace captured for the exception.
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;
    
    /// <summary>
    /// Business-level description of the exception context.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
