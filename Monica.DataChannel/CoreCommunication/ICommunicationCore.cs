using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.CoreCommunication;

public interface ICommunicationCore : IPipeEndpoint
{
    /// <summary>
    /// Initializes the communication core.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the communication core.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    Task DisposeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supported connection direction.
    /// </summary>
    /// <returns>The supported connection direction.</returns>
    EConnectionDirection SupportedConnectionDirection();

    /// <summary>
    /// Sends pipeline data through the communication core.
    /// </summary>
    /// <param name="data">The data context to send.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    Task SendDataAsync(DataContext data);
}
