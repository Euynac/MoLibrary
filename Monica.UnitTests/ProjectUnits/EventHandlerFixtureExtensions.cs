using Monica.EventBus.Abstractions.Handlers;

namespace Monica.UnitTests.ProjectUnits;

/// <summary>
/// Convenience execution helpers for event-handler ProjectUnit fixtures.
/// </summary>
public static class EventHandlerFixtureExtensions
{
    /// <summary>
    /// Invokes the distributed event handler owned by the fixture.
    /// </summary>
    public static Task HandleAsync<THandler, TEvent>(
        this ProjectUnitFixture<THandler> fixture,
        TEvent eventData)
        where THandler : class, IDistributedEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return fixture.Unit.HandleEventAsync(eventData);
    }

    /// <summary>
    /// Invokes the local event handler owned by the fixture.
    /// </summary>
    public static Task HandleLocalAsync<THandler, TEvent>(
        this ProjectUnitFixture<THandler> fixture,
        TEvent eventData)
        where THandler : class, ILocalEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return fixture.Unit.HandleEventAsync(eventData);
    }
}
