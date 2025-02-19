using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.EventBus.Abstractions;
/// <summary>
/// This <see cref="IEventHandlerFactory"/> implementation is used to get/release
/// handlers using Ioc.
/// </summary>
public class IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    : IEventHandlerFactory, IDisposable
{
    public Type HandlerType { get; } = handlerType;

    protected IServiceScopeFactory ScopeFactory { get; } = scopeFactory;

    /// <summary>
    /// Resolves handler object from Ioc container.
    /// </summary>
    /// <returns>Resolved handler object</returns>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        return new EventHandlerDisposeWrapper(
            (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType),
            () => scope.Dispose()
        );
    }

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    public void Dispose()
    {

    }
}
