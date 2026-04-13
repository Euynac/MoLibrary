namespace Monica.DependencyInjection.Models;

/// <summary>
/// Identifies the type of warning or error Monica produced while evaluating conventional registration.
/// </summary>
public enum DependencyInjectionAutoRegistrationIssueKind
{
    /// <summary>
    /// Monica detected a dependency lifetime but found no exposed service type to register.
    /// </summary>
    MissingExposedServices,

    /// <summary>
    /// Monica only exposed the concrete implementation type instead of an abstraction.
    /// </summary>
    ConcreteTypeOnlyExposure
}
