namespace Monica.DataChannel.Pipeline;

/// <summary>
/// Represents the state of a DataPipeline
/// </summary>
public enum PipelineState
{
    /// <summary>
    /// Pipeline is not processing data
    /// </summary>
    Idle,

    /// <summary>
    /// Pipeline is currently processing data
    /// </summary>
    Processing,

    /// <summary>
    /// Pipeline completed processing successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Pipeline was stopped by user
    /// </summary>
    Stopped,

    /// <summary>
    /// Pipeline encountered an error
    /// </summary>
    Error
}
