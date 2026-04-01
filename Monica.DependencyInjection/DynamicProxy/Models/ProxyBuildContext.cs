using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.DynamicProxy.Models;

/// <summary>
/// Describes a service registration that is being evaluated for dynamic-proxy interception.
/// </summary>
public sealed class ProxyBuildContext(Type implementationType, ServiceDescriptor descriptor)
{
    /// <summary>
    /// Gets the current service descriptor that may be rewritten into a proxied registration.
    /// </summary>
    public ServiceDescriptor ServiceDescriptor { get; } = descriptor;

    /// <summary>
    /// Gets the concrete implementation type resolved from the descriptor.
    /// </summary>
    public Type ImplementationType { get; } = implementationType;

    /// <summary>
    /// Gets the service type exposed by the descriptor.
    /// </summary>
    public Type ServiceType => ServiceDescriptor.ServiceType;
}
