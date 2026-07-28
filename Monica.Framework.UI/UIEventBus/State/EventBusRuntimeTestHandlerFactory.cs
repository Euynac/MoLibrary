using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// Creates runtime test handlers for row-level EventBus listener sessions.
/// </summary>
internal sealed class EventBusRuntimeTestHandlerFactory(
    IServiceScopeFactory serviceScopeFactory,
    Type eventType,
    EventSubscriptionScope scope,
    Func<object, Task> onEvent) : IEventHandlerFactory
{
    private readonly Type _eventType = eventType;
    private readonly EventSubscriptionScope _scope = scope;
    private readonly Func<object, Task> _onEvent = onEvent;

    /// <inheritdoc />
    public async ValueTask<IEventHandlerExecutionScope> CreateExecutionScopeAsync()
    {
        var serviceScope = serviceScopeFactory.CreateAsyncScope();
        try
        {
            var handlerType = _scope == EventSubscriptionScope.Local
                ? typeof(EventBusRuntimeLocalTestHandler<>).MakeGenericType(_eventType)
                : typeof(EventBusRuntimeDistributedTestHandler<>).MakeGenericType(_eventType);

            var handler = (IEventHandler)Activator.CreateInstance(handlerType, _onEvent)!;
            return new EventHandlerExecutionScope(
                handler,
                serviceScope.ServiceProvider,
                serviceScope.DisposeAsync);
        }
        catch
        {
            await serviceScope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Type? GetHandlerType()
    {
        return null;
    }
}
