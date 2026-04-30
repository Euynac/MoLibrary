namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Current state of a row-level EventBus test listener.
/// </summary>
public sealed class RuntimeTestListenerSnapshot
{
    /// <summary>
    /// Whether the selected subscription currently has an active test listener.
    /// </summary>
    public bool IsListening { get; init; }

    /// <summary>
    /// Topic currently being listened to.
    /// </summary>
    public string? TopicName { get; init; }

    /// <summary>
    /// Local time when the listener started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Number of captured messages currently retained in memory.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Retained messages ordered from oldest to newest.
    /// </summary>
    public IReadOnlyList<RuntimeTestEventMessage> Messages { get; init; } = [];
}
