namespace BuildingBlocksPlatform.EventBus.Dapr;

public class DaprEventBusOptions
{
    public string PubSubName { get; set; } = "pubsub";
    public string DaprEventBusCallback { get; set; } = "api/event-bus/dapr/event";
}
