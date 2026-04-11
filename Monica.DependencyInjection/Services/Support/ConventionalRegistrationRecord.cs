using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Models;
using Monica.DependencyInjection.Models.Internal;

namespace Monica.DependencyInjection.Services.Support;

/// <summary>
/// Stores the metadata Monica captured when conventional registration successfully applied a descriptor.
/// </summary>
internal sealed class ConventionalRegistrationRecord(
    ServiceDescriptor descriptor,
    Type sourceImplementationType,
    ServiceLifetime lifetime,
    DependencyInjectionLifetimeSource lifetimeSource,
    DependencyInjectionAutoRegistrationMode registrationMode,
    DependencyInjectionAutoRegistrationOutcome outcome,
    IReadOnlyList<ServiceIdentifier> exposedServices,
    IReadOnlyList<DependencyInjectionAutoRegistrationIssueInfo> issues)
{
    /// <summary>
    /// Gets the descriptor currently associated with the record.
    /// </summary>
    public ServiceDescriptor CurrentDescriptor { get; private set; } = descriptor;

    /// <summary>
    /// Gets the originally applied descriptor.
    /// </summary>
    public ServiceDescriptor OriginalDescriptor { get; } = descriptor;

    /// <summary>
    /// Gets the implementation type Monica discovered before descriptor rewriting.
    /// </summary>
    public Type SourceImplementationType { get; } = sourceImplementationType;

    /// <summary>
    /// Gets the lifetime Monica applied.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the source Monica used to resolve the lifetime.
    /// </summary>
    public DependencyInjectionLifetimeSource LifetimeSource { get; } = lifetimeSource;

    /// <summary>
    /// Gets the service-collection mutation mode Monica used.
    /// </summary>
    public DependencyInjectionAutoRegistrationMode RegistrationMode { get; } = registrationMode;

    /// <summary>
    /// Gets the applied result of the successful registration.
    /// </summary>
    public DependencyInjectionAutoRegistrationOutcome Outcome { get; } = outcome;

    /// <summary>
    /// Gets the full service-identity group Monica exposed for the implementation.
    /// </summary>
    public IReadOnlyList<ServiceIdentifier> ExposedServices { get; } = exposedServices;

    /// <summary>
    /// Gets the warnings Monica emitted while evaluating this registration.
    /// </summary>
    public IReadOnlyList<DependencyInjectionAutoRegistrationIssueInfo> Issues { get; } = issues;

    /// <summary>
    /// Gets whether a later step rewrote the descriptor.
    /// </summary>
    public bool WasRewritten { get; private set; }

    /// <summary>
    /// Gets the rewrite reason when <see cref="WasRewritten"/> is <see langword="true"/>.
    /// </summary>
    public string? RewriteReason { get; private set; }

    /// <summary>
    /// Associates the record with a rewritten descriptor.
    /// </summary>
    public void UpdateDescriptor(ServiceDescriptor descriptor, string rewriteReason)
    {
        CurrentDescriptor = descriptor;
        WasRewritten = true;
        RewriteReason = string.IsNullOrWhiteSpace(RewriteReason)
            ? rewriteReason
            : $"{RewriteReason}, {rewriteReason}";
    }
}
