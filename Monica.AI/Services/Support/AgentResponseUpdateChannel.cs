using System.Threading.Channels;
using Microsoft.Agents.AI;

namespace Monica.AI.Services.Support;

/// <summary>
/// Coordinates streamed agent updates from the model pipeline and synthetic tool events.
/// </summary>
internal sealed class AgentResponseUpdateChannel
{
    private readonly Channel<AgentResponseUpdate> _channel = Channel.CreateUnbounded<AgentResponseUpdate>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// Publish a single streamed update.
    /// </summary>
    public ValueTask PublishAsync(AgentResponseUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return _channel.Writer.WriteAsync(update, ct);
    }

    /// <summary>
    /// Read all published updates until the stream completes.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Complete the stream, optionally with an error.
    /// </summary>
    public void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }
}
