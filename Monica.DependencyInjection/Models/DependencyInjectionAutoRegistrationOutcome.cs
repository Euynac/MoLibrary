namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes the applied result of a successful Monica conventional registration.
/// </summary>
public enum DependencyInjectionAutoRegistrationOutcome
{
    /// <summary>
    /// The descriptor was added without replacing an existing descriptor.
    /// </summary>
    Added,

    /// <summary>
    /// The descriptor replaced an existing descriptor with the same service identity.
    /// </summary>
    ReplacedExisting
}
