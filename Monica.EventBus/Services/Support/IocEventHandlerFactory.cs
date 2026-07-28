using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;

namespace Monica.EventBus.Services.Support;

/// <summary>
/// Factory for creating event handlers from the IoC container.
/// </summary>
public class IocEventHandlerFactory(IServiceScopeFactory serviceScopeFactory, Type handlerType) : IEventHandlerFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    private readonly Type _handlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));

    /// <inheritdoc />
    public async ValueTask<IEventHandlerExecutionScope> CreateExecutionScopeAsync()
    {
        var scope = _serviceScopeFactory.CreateAsyncScope();
        try
        {
            var handler = (IEventHandler)scope.ServiceProvider.GetRequiredService(_handlerType);
            return new EventHandlerExecutionScope(handler, scope.ServiceProvider, scope.DisposeAsync);
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Type GetHandlerType() => _handlerType;
}
