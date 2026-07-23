using Monica.EventBus.Abstractions.Handlers;

namespace Monica.Testing.ProjectUnits;

/// <summary>
/// Convenience execution helpers for event-handler ProjectUnit fixtures.
/// </summary>
public static class EventHandlerFixtureExtensions
{
    /// <summary>
    /// Invokes the distributed event handler owned by the fixture.
    /// </summary>
    /// <param name="fixture">The fixture that owns the handler.</param>
    /// <param name="eventData">The event payload.</param>
    /// <param name="cancellationToken">Signals that the test no longer needs the handler result.</param>
    public static Task HandleAsync<THandler, TEvent>(
        this ProjectUnitFixture<THandler> fixture,
        TEvent eventData,
        CancellationToken cancellationToken = default)
        where THandler : class, IDistributedEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return fixture.Unit.HandleEventAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Invokes the local event handler owned by the fixture.
    /// </summary>
    /// <param name="fixture">The fixture that owns the handler.</param>
    /// <param name="eventData">The event payload.</param>
    /// <param name="cancellationToken">Signals that the test no longer needs the handler result.</param>
    public static Task HandleLocalAsync<THandler, TEvent>(
        this ProjectUnitFixture<THandler> fixture,
        TEvent eventData,
        CancellationToken cancellationToken = default)
        where THandler : class, ILocalEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return fixture.Unit.HandleEventAsync(eventData, cancellationToken);
    }
}
