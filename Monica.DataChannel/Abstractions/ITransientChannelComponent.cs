namespace Monica.DataChannel.Abstractions;

/// <summary>
/// Marker interface for components that use a transient lifetime.
/// Implementing components are resolved again from the <see cref="IServiceProvider"/> for each use.
/// <c>Dispose</c> and <c>Init</c> are invoked only once in a thread-safe manner.
/// Because instances are recreated and released, preserve shared state explicitly in a host-scoped dependency.
/// </summary>
public interface ITransientChannelComponent
{
}
