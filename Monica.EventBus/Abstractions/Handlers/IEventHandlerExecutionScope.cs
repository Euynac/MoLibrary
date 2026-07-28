namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Owns one resolved event handler and the dependency-injection scope used by its execution pipeline.
/// </summary>
/// <remarks>
/// Consumers must dispose this scope asynchronously because handlers, pipeline behaviors, and their dependencies may
/// implement only <see cref="IAsyncDisposable"/>.
/// </remarks>
public interface IEventHandlerExecutionScope : IAsyncDisposable
{
    /// <summary>
    /// Gets the handler instance selected for this delivery.
    /// </summary>
    IEventHandler EventHandler { get; }

    /// <summary>
    /// Gets the provider that owns both the handler and its execution-pipeline behaviors.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
