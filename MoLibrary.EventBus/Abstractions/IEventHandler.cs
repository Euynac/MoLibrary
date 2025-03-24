namespace MoLibrary.EventBus.Abstractions;

public interface IEventHandler<in TEvent> : IEventHandler
{
    
}

public interface IEventHandler
{

}