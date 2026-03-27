using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel;

/// <summary>
/// Represents a data channel.
/// Wraps a data pipeline and provides a unified access and control surface.
/// Acts as the managed unit tracked by <see cref="DataChannelCentral"/>.
/// </summary>
/// <param name="pipeline">The data pipeline instance.</param>
public class DataChannel(DataPipeline pipeline)
{
    /// <summary>
    /// Gets the underlying data pipeline.
    /// Contains the core data transfer and processing logic.
    /// </summary>
    public DataPipeline Pipe { get; } = pipeline;

    /// <summary>
    /// Gets the unique identifier of the data channel.
    /// This value matches the pipeline identifier.
    /// </summary>
    public string Id => Pipe.Id;

    /// <summary>
    /// Reinitializes the data channel.
    /// Call this when the channel needs to be reset or reconnected.
    /// </summary>
    public async Task ReInitialize(CancellationToken cancellationToken = default)
    {
        await Pipe.InitAsync(cancellationToken);
    }

    /// <summary>
    /// Sends data from the inner endpoint.
    /// The payload passes through transform middleware, if any, and is then received by the outer endpoint.
    /// </summary>
    /// <param name="data">The data to send.</param>
    public async Task SendDataFromInnerAsync(object data)
    {
        await Pipe.SendDataAsync(new DataContext(EDataSource.Inner, data));
    }

    /// <summary>
    /// Sends data from the outer endpoint.
    /// The payload passes through transform middleware, if any, and is then received by the inner endpoint.
    /// </summary>
    /// <param name="data">The data to send.</param>
    public async Task SendDataFromOuterAsync(object data)
    {
        await Pipe.SendDataAsync(new DataContext(EDataSource.Outer, data));
    }
}
