namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes one post-registration rewrite applied to a service descriptor.
/// </summary>
public sealed class DependencyInjectionDescriptorRewriteInfo
{
    /// <summary>
    /// Gets the module or feature that rewrote the descriptor.
    /// </summary>
    public required string SourceModule { get; init; }

    /// <summary>
    /// Gets the short summary shown in diagnostics lists.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the rewrite strategy, such as dynamic-proxy application.
    /// </summary>
    public string? RewriteKind { get; init; }

    /// <summary>
    /// Gets the proxy kind when the rewrite created a proxy.
    /// </summary>
    public string? ProxyKind { get; init; }

    /// <summary>
    /// Gets the registration style of the original descriptor before rewriting.
    /// </summary>
    public string? RegistrationStyle { get; init; }

    /// <summary>
    /// Gets the full implementation type name evaluated during the rewrite.
    /// </summary>
    public string? ImplementationType { get; init; }

    /// <summary>
    /// Gets the compact implementation type name evaluated during the rewrite.
    /// </summary>
    public string? ImplementationTypeDisplayName { get; init; }

    /// <summary>
    /// Gets the implementation assembly name evaluated during the rewrite.
    /// </summary>
    public string? ImplementationAssemblyName { get; init; }

    /// <summary>
    /// Gets whether the rewritten implementation required cached service-provider injection.
    /// </summary>
    public bool ShouldInjectCachedServiceProvider { get; init; }

    /// <summary>
    /// Gets the full interceptor type names applied by the rewrite.
    /// </summary>
    public IReadOnlyList<string> InterceptorTypes { get; init; } = [];

    /// <summary>
    /// Gets the compact interceptor type names applied by the rewrite.
    /// </summary>
    public IReadOnlyList<string> InterceptorTypeDisplayNames { get; init; } = [];
}
