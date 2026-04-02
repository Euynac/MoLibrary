using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoMapper;
using Monica.Core.Logging;
using Monica.DependencyInjection.Abstractions;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.DomainDrivenDesign;

/// <summary>
/// Base class for event handlers, providing common properties and methods.
/// </summary>
public abstract class EventHandlerBase :
    ITransientDependency,
    ICachedServiceProviderAccessor
{
    private readonly Lazy<ILogger> _loggerLazy;

    protected EventHandlerBase()
    {
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    public ICachedServiceProvider CachedServiceProvider
    {
        get => field ?? throw CreateNotInitializedException();
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected ILogger Logger => _loggerLazy.Value;

    protected IMoMapper Mapper => CachedServiceProvider.GetRequiredService<IMoMapper>();

    private InvalidOperationException CreateNotInitializedException()
    {
        return new InvalidOperationException(
            $"Cached service provider is not initialized for {GetType().FullName}. Resolve the service through Monica DI instead of constructing it manually.");
    }
}


/// <summary>
/// Base class for distributed domain event handlers.
/// </summary>
/// <typeparam name="TEvent">The event payload type.</typeparam>
public abstract class DomainEventHandler<TEvent> :
    EventHandlerBase,
    IDistributedEventHandler<TEvent>
{
    /// <summary>
    /// Handles the distributed event.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    public abstract Task HandleEventAsync(TEvent eventData);
}

/// <summary>
/// Base class for local event handlers.
/// </summary>
/// <typeparam name="TEvent">The event payload type.</typeparam>
public abstract class LocalEventHandler<TEvent> :
    EventHandlerBase,
    ILocalEventHandler<TEvent>
{
    /// <summary>
    /// Handles the local event.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    public abstract Task HandleEventAsync(TEvent eventData);
}
