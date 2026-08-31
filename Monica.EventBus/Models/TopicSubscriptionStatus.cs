using Monica.EventBus.Abstractions;

namespace Monica.EventBus.Models;

/// <summary>
/// A single recorded error for a topic subscription, kept in the bounded
/// <see cref="TopicSubscriptionStatus.RecentErrors"/> history.
/// </summary>
public sealed record TopicSubscriptionError(
    DateTime OccurredAt,
    string Message,
    string? ExceptionText);

/// <summary>
/// Immutable snapshot of the runtime health of one external topic subscription,
/// keyed by (<see cref="ServiceKey"/>, <see cref="TopicName"/>).
/// Snapshots are produced by <c>ITopicSubscriptionStatusStore</c> on every read or change
/// notification and are safe to share across threads.
/// </summary>
public sealed record TopicSubscriptionStatus
{
    /// <summary>
    /// Gets the service key of the distributed event bus instance owning the topic
    /// subscription, or <see langword="null"/> for the default instance.
    /// </summary>
    public required string? ServiceKey { get; init; }

    /// <summary>
    /// Gets the topic name the external subscription is bound to.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Gets the provider kind that reported this status.
    /// </summary>
    public required EventBusProviderKind Provider { get; init; }

    /// <summary>
    /// Gets the current runtime state of the external subscription.
    /// </summary>
    public required TopicSubscriptionRuntimeState State { get; init; }

    /// <summary>
    /// Gets the UTC time at which the current <see cref="State"/> was entered.
    /// </summary>
    public required DateTime StateChangedAt { get; init; }

    /// <summary>
    /// Gets the UTC time of the most recent reported error, if any.
    /// </summary>
    public DateTime? LastErrorAt { get; init; }

    /// <summary>
    /// Gets the message of the most recent reported error, if any.
    /// Retained across recoveries for diagnosis; it does not imply the current state is unhealthy.
    /// </summary>
    public string? LastErrorMessage { get; init; }

    /// <summary>
    /// Gets the full text of the most recent reported exception, if any.
    /// </summary>
    public string? LastErrorException { get; init; }

    /// <summary>
    /// Gets the number of consecutive failed connection attempts since the last stable period.
    /// Reset to zero when the subscription becomes healthy again.
    /// </summary>
    public required int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Gets the total number of recovery cycles (failure + reconnect) observed for this topic.
    /// </summary>
    public required long RecoveryCount { get; init; }

    /// <summary>
    /// Gets the total number of message-level errors (deserialization, handler, deadline) reported
    /// for this topic. These errors do not change the runtime state by themselves.
    /// </summary>
    public required long MessageErrorCount { get; init; }

    /// <summary>
    /// Gets the total number of messages successfully handled for this topic.
    /// Updated with a lock-free counter on every handled message and therefore read as of the
    /// last snapshot rather than real-time exact.
    /// </summary>
    public required long ProcessedMessages { get; init; }

    /// <summary>
    /// Gets the UTC time of the most recently handled message, if any. Useful to distinguish a
    /// healthy-but-idle topic from one that stopped receiving.
    /// </summary>
    public DateTime? LastMessageReceivedAt { get; init; }

    /// <summary>
    /// Gets the most recent errors in chronological order (oldest first), bounded by the store.
    /// </summary>
    public required IReadOnlyList<TopicSubscriptionError> RecentErrors { get; init; }
}
