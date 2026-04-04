namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Aggregated exception statistics for the data channel layer.
/// </summary>
public class DataChannelExceptionSummaryInfo
{
    /// <summary>
    /// Total number of registered channels.
    /// </summary>
    public int TotalChannels { get; set; }
    
    /// <summary>
    /// Channels currently reporting exceptions.
    /// </summary>
    public int ChannelsWithExceptions { get; set; }
    
    /// <summary>
    /// Sum of active exception counts across all channels.
    /// </summary>
    public int TotalCurrentExceptions { get; set; }

    /// <summary>
    /// Accumulated number of exception events recorded historically.
    /// </summary>
    public int TotalHistoricalExceptions { get; set; }
    
    /// <summary>
    /// Detailed statistics per channel.
    /// </summary>
    public List<DataChannelExceptionSummaryItem> ChannelSummaries { get; set; } = new();
}

/// <summary>
/// Per-channel exception statistics.
/// </summary>
public class DataChannelExceptionSummaryItem
{
    /// <summary>
    /// Identifier of the DataChannel.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;
    
    /// <summary>
    /// Identifier of the associated pipeline.
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;
    
    /// <summary>
    /// Active exception count for the channel.
    /// </summary>
    public long CurrentExceptionCount { get; set; }

    /// <summary>
    /// Total exception count recorded.
    /// </summary>
    public long TotalExceptionCount { get; set; }
    
    /// <summary>
    /// Maximum size of the exception history pool.
    /// </summary>
    public int MaxPoolSize { get; set; }
    
    /// <summary>
    /// Indicates whether the channel currently has exceptions.
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// Timestamp of the latest recorded exception, if any.
    /// </summary>
    public DateTime? LatestException { get; set; }
}
