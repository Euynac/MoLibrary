using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Abstractions.Partitioning;

/// <summary>
/// Resolves a logical ordering partition for a data channel message.
/// </summary>
public interface IDataChannelPartitionKeyResolver
{
    /// <summary>
    /// Resolves the logical partition key for the supplied channel context.
    /// Returning <see langword="null"/> or whitespace keeps transport behavior unchanged.
    /// Implementations should be deterministic and side-effect free.
    /// </summary>
    /// <param name="context">The channel data context.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous resolvers.</param>
    /// <returns>The partition key to use, or <see langword="null"/> when no partition applies.</returns>
    ValueTask<string?> ResolvePartitionKeyAsync(
        ChannelDataContext context,
        CancellationToken cancellationToken = default);
}
