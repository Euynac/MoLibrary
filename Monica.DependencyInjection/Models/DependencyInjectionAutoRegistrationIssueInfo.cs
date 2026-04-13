using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes one warning or error Monica emitted while evaluating conventional registration.
/// </summary>
public sealed class DependencyInjectionAutoRegistrationIssueInfo
{
    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public required DependencyInjectionDiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the issue kind Monica detected.
    /// </summary>
    public required DependencyInjectionAutoRegistrationIssueKind Kind { get; init; }

    /// <summary>
    /// Gets the full implementation type name Monica evaluated.
    /// </summary>
    public required string SourceImplementationType { get; init; }

    /// <summary>
    /// Gets the compact display name of the evaluated implementation type.
    /// </summary>
    public required string SourceImplementationTypeDisplayName { get; init; }

    /// <summary>
    /// Gets the assembly name containing the evaluated implementation type.
    /// </summary>
    public string? SourceImplementationAssemblyName { get; init; }

    /// <summary>
    /// Gets the lifetime Monica resolved for the implementation.
    /// </summary>
    public required ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// Gets the source Monica used to determine the lifetime.
    /// </summary>
    public required DependencyInjectionLifetimeSource LifetimeSource { get; init; }

    /// <summary>
    /// Gets the exposed service identities Monica had available while evaluating the implementation.
    /// </summary>
    public IReadOnlyList<DependencyInjectionExposedServiceInfo> ExposedServices { get; init; } = [];
}
