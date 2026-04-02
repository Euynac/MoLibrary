namespace Monica.EventBus.Annotations;

public interface IEventNameProvider
{
    string GetName(Type eventType);
}
