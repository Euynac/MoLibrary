using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;

namespace Monica.EventBus.Services.Support;
/// <summary>
/// Adapter that allows a delegate to be used as an <see cref="ILocalEventHandler{TEvent}"/>
/// implementation.
/// </summary>
/// <typeparam name="TEvent">Event type.</typeparam>
public class ActionEventHandler<TEvent>(Func<TEvent, CancellationToken, Task> action)
    : ILocalEventHandler<TEvent>, IDistributedEventHandler<TEvent>
{
    /// <summary>
    /// Delegate used to handle the event.
    /// </summary>
    public Func<TEvent, CancellationToken, Task> Action { get; } =
        action ?? throw new ArgumentNullException(nameof(action));

    /// <summary>
    /// Handles the event by invoking the configured delegate.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    /// <param name="cancellationToken">Signals that the event delivery is no longer waiting.</param>
    public Task HandleEventAsync(TEvent eventData, CancellationToken cancellationToken)
    {
        return Action(eventData, cancellationToken);
    }
}

/// <summary>
/// Factory for delegate-based event handlers.
/// </summary>
internal class ActionEventHandlerFactory<TEvent>(
    Func<TEvent, CancellationToken, Task> action,
    IServiceScopeFactory serviceScopeFactory) : IEventHandlerFactory
    where TEvent : class
{
    private readonly Func<TEvent, CancellationToken, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    /// <inheritdoc />
    public ValueTask<IEventHandlerExecutionScope> CreateExecutionScopeAsync()
    {
        var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = new ActionEventHandler<TEvent>(_action);
        return ValueTask.FromResult<IEventHandlerExecutionScope>(
            new EventHandlerExecutionScope(handler, scope.ServiceProvider, scope.DisposeAsync));
    }

    /// <inheritdoc />
    public Type? GetHandlerType()
    {
        return null;
    }
}
