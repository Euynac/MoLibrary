using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.EventBus.Abstractions.Handlers;

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

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(f => f._handlerType == _handlerType);
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
