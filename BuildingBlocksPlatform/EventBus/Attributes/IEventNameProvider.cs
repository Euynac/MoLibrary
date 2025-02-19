namespace BuildingBlocksPlatform.EventBus.Attributes;

public interface IEventNameProvider
{
    string GetName(Type eventType);
}
