namespace Monica.DataChannel.Interfaces;

/// <summary>
/// Marker interface for components that use a transient lifetime.
/// Implementing components are resolved again from the <see cref="IServiceProvider"/> for each use.
/// <c>Dispose</c> and <c>Init</c> are invoked only once in a thread-safe manner.
/// Because instances are recreated and released, preserve shared state explicitly, for example by using static storage when necessary.
/// </summary>
public interface IComponentTransient
{
}
