using Monica.Core.Modularity.Annotations;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// Runtime local event handler used by EventBus UI test listeners.
/// </summary>
/// <typeparam name="TEvent">Event payload type captured by the listener.</typeparam>
[ExcludeFromBusinessTypeDiscovery]
internal sealed class EventBusRuntimeLocalTestHandler<TEvent>(Func<object, Task> onEvent) : ILocalEventHandler<TEvent>
{
    private readonly Func<object, Task> _onEvent = onEvent;

    /// <summary>
    /// Captures a locally delivered event payload.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    public Task HandleEventAsync(TEvent eventData)
    {
        return _onEvent(eventData!);
    }
}

/// <summary>
/// Runtime distributed event handler used by EventBus UI test listeners.
/// </summary>
/// <typeparam name="TEvent">Event payload type captured by the listener.</typeparam>
[ExcludeFromBusinessTypeDiscovery]
internal sealed class EventBusRuntimeDistributedTestHandler<TEvent>(Func<object, Task> onEvent) : IDistributedEventHandler<TEvent>
{
    private readonly Func<object, Task> _onEvent = onEvent;

    /// <summary>
    /// Captures a distributed event payload.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    public Task HandleEventAsync(TEvent eventData)
    {
        return _onEvent(eventData!);
    }
}
