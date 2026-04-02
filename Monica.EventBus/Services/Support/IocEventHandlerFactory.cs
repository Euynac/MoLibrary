using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Services.Support;

/// <summary>
/// Factory for creating event handlers from the IoC container.
/// </summary>
public class IocEventHandlerFactory(IServiceScopeFactory serviceScopeFactory, Type handlerType) : IEventHandlerFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    private readonly Type _handlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));

    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = _serviceScopeFactory.CreateScope();
        var handler = (IEventHandler)scope.ServiceProvider.GetRequiredService(_handlerType);
        return new IocEventHandlerDisposeWrapper(handler, scope);
    }

    public Type GetHandlerType() => _handlerType;

    private class IocEventHandlerDisposeWrapper(IEventHandler eventHandler, IServiceScope scope)
        : IEventHandlerDisposeWrapper
    {
        public IEventHandler EventHandler { get; } = eventHandler;

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
