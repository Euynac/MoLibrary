using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes why a final service descriptor is associated with Monica conventional registration.
/// </summary>
public sealed class DependencyInjectionAutoRegistrationInfo
{
    /// <summary>
    /// Gets the implementation type Monica discovered during conventional registration.
    /// </summary>
    public required string SourceImplementationType { get; init; }

    /// <summary>
    /// Gets the compact display name of the discovered implementation type.
    /// </summary>
    public required string SourceImplementationTypeDisplayName { get; init; }

    /// <summary>
    /// Gets the implementation assembly name.
    /// </summary>
    public string? SourceImplementationAssemblyName { get; init; }

    /// <summary>
    /// Gets the lifetime Monica applied when it created the descriptor.
    /// </summary>
    public required ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// Gets the source Monica used to determine the lifetime.
    /// </summary>
    public required DependencyInjectionLifetimeSource LifetimeSource { get; init; }

    /// <summary>
    /// Gets the collection mutation mode Monica used.
    /// </summary>
    public required DependencyInjectionAutoRegistrationMode RegistrationMode { get; init; }

    /// <summary>
    /// Gets the applied registration outcome.
    /// </summary>
    public required DependencyInjectionAutoRegistrationOutcome Outcome { get; init; }

    /// <summary>
    /// Gets every service identity Monica exposed for the same implementation.
    /// </summary>
    public IReadOnlyList<DependencyInjectionExposedServiceInfo> ExposedServices { get; init; } = [];

    /// <summary>
    /// Gets the warnings Monica emitted while evaluating this automatic registration.
    /// </summary>
    public IReadOnlyList<DependencyInjectionAutoRegistrationIssueInfo> Issues { get; init; } = [];
}
