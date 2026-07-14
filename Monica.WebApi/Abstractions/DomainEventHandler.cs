using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.WebApi.Abstractions;

/// <summary>
/// Base class for event handlers, providing common properties and methods.
/// </summary>
public abstract class EventHandlerBase :
    ITransientDependency,
    ICachedServiceProviderAccessor
{
    /// <summary>
    /// Initializes an event handler with logging owned by the current host.
    /// </summary>
    /// <param name="loggerFactory">The host logger factory.</param>
    protected EventHandlerBase(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public ICachedServiceProvider CachedServiceProvider
    {
        get => field ?? throw CreateNotInitializedException();
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected ILogger Logger { get; }

    protected IObjectMapper Mapper => CachedServiceProvider.GetRequiredService<IObjectMapper>();

    private InvalidOperationException CreateNotInitializedException()
    {
        return new InvalidOperationException(
            $"Cached service provider is not initialized for {GetType().FullName}. Resolve the service through Monica DI instead of constructing it manually.");
    }
}


/// <summary>
/// Base class for distributed domain event handlers.
/// </summary>
/// <remarks>
/// Monica matches distributed handlers by exact event type and topic. Do not use a base
/// event type as a catch-all listener for derived events.
/// </remarks>
/// <typeparam name="TEvent">The event payload type.</typeparam>
/// <param name="loggerFactory">The host logger factory.</param>
public abstract class DomainEventHandler<TEvent>(ILoggerFactory loggerFactory) :
    EventHandlerBase(loggerFactory),
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
/// <remarks>
/// Monica matches local handlers by exact event type and topic. Do not use a base event
/// type as a catch-all listener for derived events.
/// </remarks>
/// <typeparam name="TEvent">The event payload type.</typeparam>
/// <param name="loggerFactory">The host logger factory.</param>
public abstract class LocalEventHandler<TEvent>(ILoggerFactory loggerFactory) :
    EventHandlerBase(loggerFactory),
    ILocalEventHandler<TEvent>
{
    /// <summary>
    /// Handles the local event.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    public abstract Task HandleEventAsync(TEvent eventData);
}
