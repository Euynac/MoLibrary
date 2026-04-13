using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes one final <see cref="ServiceDescriptor"/> entry from the built service collection.
/// </summary>
public sealed class DependencyInjectionDescriptorInfo
{
    /// <summary>
    /// Gets the descriptor index inside the final service collection.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the full service type name.
    /// </summary>
    public required string ServiceType { get; init; }

    /// <summary>
    /// Gets the compact service type display name.
    /// </summary>
    public required string ServiceTypeDisplayName { get; init; }

    /// <summary>
    /// Gets the service type assembly name.
    /// </summary>
    public string? ServiceAssemblyName { get; init; }

    /// <summary>
    /// Gets the registered service lifetime.
    /// </summary>
    public required ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// Gets whether the descriptor is keyed.
    /// </summary>
    public required bool IsKeyedService { get; init; }

    /// <summary>
    /// Gets the key display text when the descriptor is keyed.
    /// </summary>
    public string? ServiceKey { get; init; }

    /// <summary>
    /// Gets how the descriptor creates its implementation.
    /// </summary>
    public required DependencyInjectionDescriptorImplementationKind ImplementationKind { get; init; }

    /// <summary>
    /// Gets the full implementation type name when it can be resolved without invoking factories.
    /// </summary>
    public string? ImplementationType { get; init; }

    /// <summary>
    /// Gets the compact implementation type display name when available.
    /// </summary>
    public string? ImplementationTypeDisplayName { get; init; }

    /// <summary>
    /// Gets the implementation assembly name when available.
    /// </summary>
    public string? ImplementationAssemblyName { get; init; }

    /// <summary>
    /// Gets a readable factory signature when the descriptor uses a factory delegate.
    /// </summary>
    public string? FactoryDisplay { get; init; }

    /// <summary>
    /// Gets whether the descriptor exposes an open generic service or implementation.
    /// </summary>
    public bool IsOpenGeneric { get; init; }

    /// <summary>
    /// Gets whether Monica conventional registration created this descriptor.
    /// </summary>
    public bool IsAutoRegistered { get; init; }

    /// <summary>
    /// Gets the associated conventional-registration details when available.
    /// </summary>
    public DependencyInjectionAutoRegistrationInfo? AutoRegistration { get; init; }

    /// <summary>
    /// Gets whether the descriptor has captured warning-level automatic-registration issues.
    /// </summary>
    public bool HasWarningIssues { get; init; }

    /// <summary>
    /// Gets whether the descriptor has captured error-level automatic-registration issues.
    /// </summary>
    public bool HasErrorIssues { get; init; }

    /// <summary>
    /// Gets whether a later step rewrote the descriptor after conventional registration.
    /// </summary>
    public bool WasRewritten { get; init; }

    /// <summary>
    /// Gets the captured rewrite steps when the descriptor was rewritten.
    /// </summary>
    public IReadOnlyList<DependencyInjectionDescriptorRewriteInfo> Rewrites { get; init; } = [];

    /// <summary>
    /// Gets the rewrite reason when <see cref="WasRewritten"/> is <see langword="true"/>.
    /// </summary>
    public string? RewriteReason { get; init; }
}
