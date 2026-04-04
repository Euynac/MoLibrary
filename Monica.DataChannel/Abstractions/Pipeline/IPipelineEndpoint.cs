using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Abstractions.Pipeline;

/// <summary>
/// Contract for pipeline endpoints.
/// Defines the ingress and egress points of a data pipeline and is responsible for receiving and handling data.
/// Implementations can act as a data source, a destination, or both.
/// </summary>
public interface IPipelineEndpoint : IPipelineAware, IPipelineComponent
{
    /// <summary>
    /// Receives data from the pipeline.
    /// The pipeline calls this method when data reaches the endpoint.
    /// </summary>
    /// <param name="data">The data context to process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReceiveDataAsync(ChannelDataContext data);

    /// <summary>
    /// Gets or sets the endpoint direction.
    /// Indicates whether this endpoint is the inner side or the outer side of the pipeline.
    /// </summary>
    public ChannelSide EntranceType { get; internal set; }
}
