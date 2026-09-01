namespace Monica.EventBus.Models;

/// <summary>
/// Runtime health states of an external topic subscription managed by a distributed provider
/// (for example a Dapr streaming subscription or a Kafka consumer loop).
/// This is independent of the registration state machine (<see cref="EventSubscriptionState"/>):
/// a subscription can be registered as <see cref="EventSubscriptionState.Active"/> while its
/// underlying topic connection is recovering or has failed.
/// </summary>
public enum TopicSubscriptionRuntimeState
{
    /// <summary>
    /// The external subscription is being established; it has not proven stable yet.
    /// </summary>
    Subscribing = 0,

    /// <summary>
    /// The external subscription is established, stable, and receiving messages.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// The external subscription failed and is waiting to reconnect with backoff.
    /// </summary>
    Recovering = 2,

    /// <summary>
    /// The external subscription supervisor or consumer loop terminated and is not retrying.
    /// Messages on this topic are not being delivered until the application restarts it.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The external subscription was removed cleanly (no subscriptions remain for the topic).
    /// </summary>
    Stopped = 4
}
