namespace Monica.EventBus.Abstractions.Handlers;

public interface IEventHandler<in TEvent> : IEventHandler
{
    
}

public interface IEventHandler
{

}