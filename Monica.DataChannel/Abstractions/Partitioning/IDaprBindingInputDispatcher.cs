using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Abstractions.Partitioning;

/// <summary>
/// Allows a Dapr input binding to hand an incoming message to a custom dispatcher before the pipeline continues.
/// </summary>
public interface IDaprBindingInputDispatcher
{
    /// <summary>
    /// Attempts to dispatch the incoming input-binding payload.
    /// Return <see langword="true"/> when the dispatcher has accepted responsibility for the message and the
    /// Dapr HTTP handler can complete immediately. Return <see langword="false"/> to let the normal pipeline run.
    /// </summary>
    /// <param name="context">The incoming channel data context.</param>
    /// <param name="next">The existing pipeline continuation.</param>
    /// <param name="cancellationToken">A cancellation token for the current HTTP request.</param>
    /// <returns><see langword="true"/> when the message was handled by the dispatcher; otherwise <see langword="false"/>.</returns>
    Task<bool> TryDispatchAsync(
        ChannelDataContext context,
        Func<ChannelDataContext, CancellationToken, Task> next,
        CancellationToken cancellationToken = default);
}
