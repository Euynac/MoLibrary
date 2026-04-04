namespace Monica.DataChannel.Abstractions.Pipeline;

/// <summary>
/// Base contract for pipeline components.
/// Shared by all pipeline-related components, including endpoints and middleware.
/// Provides the basic ability to expose component metadata.
/// </summary>
public interface IPipelineComponent
{
    /// <summary>
    /// Gets metadata for the pipeline component.
    /// Returns dynamic properties such as configuration data and runtime state.
    /// </summary>
    /// <returns>A dynamic object that contains component metadata.</returns>
    public dynamic GetMetadata();
}
