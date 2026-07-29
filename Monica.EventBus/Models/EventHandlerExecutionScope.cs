using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Models;

/// <summary>
/// Owns an event handler, its service provider, and the asynchronous release operation for that scope.
/// </summary>
public sealed class EventHandlerExecutionScope : IEventHandlerExecutionScope
{
    private Func<ValueTask>? _disposeAsync;

    /// <summary>
    /// Initializes an event-handler execution scope.
    /// </summary>
    /// <param name="eventHandler">The handler resolved for the current delivery.</param>
    /// <param name="serviceProvider">The scoped provider that owns the handler and pipeline behaviors.</param>
    /// <param name="disposeAsync">The operation that asynchronously releases the owning scope.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    public EventHandlerExecutionScope(
        IEventHandler eventHandler,
        IServiceProvider serviceProvider,
        Func<ValueTask> disposeAsync)
    {
        EventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _disposeAsync = disposeAsync ?? throw new ArgumentNullException(nameof(disposeAsync));
    }

    /// <inheritdoc />
    public IEventHandler EventHandler { get; }

    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        var disposeAsync = Interlocked.Exchange(ref _disposeAsync, null);
        return disposeAsync?.Invoke() ?? ValueTask.CompletedTask;
    }
}
