namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes how Monica conventional registration resolved the service lifetime.
/// </summary>
public enum DependencyInjectionLifetimeSource
{
    /// <summary>
    /// The lifetime source could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// The lifetime came from <c>DependencyAttribute.Lifetime</c>.
    /// </summary>
    DependencyAttribute,

    /// <summary>
    /// The lifetime came from <c>ITransientDependency</c>.
    /// </summary>
    TransientMarkerInterface,

    /// <summary>
    /// The lifetime came from <c>IScopedDependency</c>.
    /// </summary>
    ScopedMarkerInterface,

    /// <summary>
    /// The lifetime came from <c>ISingletonDependency</c>.
    /// </summary>
    SingletonMarkerInterface
}
