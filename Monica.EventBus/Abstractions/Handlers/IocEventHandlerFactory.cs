using Microsoft.Extensions.DependencyInjection;

namespace Monica.EventBus.Abstractions.Handlers;

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
        var handler = (IMoEventHandler)scope.ServiceProvider.GetRequiredService(_handlerType);
        return new IocEventHandlerDisposeWrapper(handler, scope);
    }

    public Type GetHandlerType() => _handlerType;

    private class IocEventHandlerDisposeWrapper(IMoEventHandler eventHandler, IServiceScope scope)
        : IEventHandlerDisposeWrapper
    {
        public IMoEventHandler EventHandler { get; } = eventHandler;

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
