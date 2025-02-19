namespace BuildingBlocksPlatform.EventBus.Abstractions;

public interface IEventHandler<in TEvent> : IEventHandler
{
    
}

public interface IEventHandler
{

}