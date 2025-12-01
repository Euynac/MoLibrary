using MoLibrary.Dapr.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Modules;

namespace MoLibrary.Dapr.EventBus.Models;

/// <summary>
/// This class defines subscribe endpoint response
/// </summary>
internal class MoSubscription
{
    public static IEnumerable<MoSubscription> GetMoSubscriptions(ModuleEventBusOption option,
        ModuleDaprEventBusOption busOption)
    {
        var result = new List<MoSubscription>();

        // Use pre-computed registration information - ZERO REFLECTION!
        var distributedHandlers = option.EventHandlers.Where(h => h.IsDistributed);

        foreach (var handlerInfo in distributedHandlers)
        {
            var subscription = new MoSubscription
            {
                PubsubName = busOption.PubSubName,
                Topic = handlerInfo.TopicName, // Pre-computed topic name!
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