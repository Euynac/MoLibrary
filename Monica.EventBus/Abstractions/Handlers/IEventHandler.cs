namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Identifies an event handler for a specific payload type.
/// </summary>
/// <typeparam name="TEvent">The handled event payload type.</typeparam>
public interface IEventHandler<in TEvent> : IEventHandler;

/// <summary>
/// Provides the non-generic event-handler contract used by runtime dispatch.
/// </summary>
public interface IEventHandler;
