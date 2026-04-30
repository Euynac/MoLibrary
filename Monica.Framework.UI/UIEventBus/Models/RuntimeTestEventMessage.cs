namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Captured payload from a row-level EventBus test listener.
/// </summary>
public sealed class RuntimeTestEventMessage
{
    /// <summary>
    /// Local time when the listener received the event.
    /// </summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>
    /// Topic on which the event was received.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Full CLR name of the event type received by the listener.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// JSON representation of the received payload.
    /// </summary>
    public required string PayloadJson { get; init; }
}
