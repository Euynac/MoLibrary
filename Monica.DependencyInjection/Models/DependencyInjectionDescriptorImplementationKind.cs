namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes how a service descriptor creates its implementation.
/// </summary>
public enum DependencyInjectionDescriptorImplementationKind
{
    /// <summary>
    /// The descriptor references an implementation type directly.
    /// </summary>
    Type,

    /// <summary>
    /// The descriptor creates the implementation through a factory delegate.
    /// </summary>
    Factory,

    /// <summary>
    /// The descriptor uses a pre-created implementation instance.
    /// </summary>
    Instance
}
