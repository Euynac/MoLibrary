namespace MoLibrary.EventBus.Attributes;

public interface IEventNameProvider
{
    string GetName(Type eventType);
}
