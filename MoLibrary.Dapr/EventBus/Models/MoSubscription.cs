using MoLibrary.Dapr.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;

namespace MoLibrary.Dapr.EventBus.Models;

/// <summary>
/// This class defines subscribe endpoint response
/// </summary>
internal class MoSubscription
{
    public static IEnumerable<MoSubscription> GetMoSubscriptions(DistributedEventBusOptions option,
        ModuleDaprEventBusOption busOption)
    {
        var result = new List<MoSubscription>();
        foreach (var handler in option.Handlers)
        {
            foreach (var @interface in handler.GetInterfaces().Where(x =>
                         x.IsGenericType && x.GetGenericTypeDefinition() ==
                         typeof(IMoDistributedEventHandler<>)))
            {
                var eventType = @interface.GetGenericArguments()[0];
                var eventName = EventNameAttribute.GetNameOrDefault(eventType);

                var subscription = new MoSubscription
                {
                    PubsubName = busOption.PubSubName,
                    Topic = eventName,
                    Route = busOption.DaprEventBusCallback,
                    Metadata = new MoMetadata
                    {
                        {
                            "rawPayload", "true"
                        }
                    }
                };
                result.Add(subscription);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Topic { get; set; } = default!;

    /// <summary>
    /// Gets or sets the pubsub name
    /// </summary>
    public string PubsubName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the route
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Gets or sets the routes
    /// </summary>
    public MoRoutes? Routes { get; set; }

    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    public MoMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the deadletter topic.
    /// </summary>
    public string? DeadLetterTopic { get; set; }
}