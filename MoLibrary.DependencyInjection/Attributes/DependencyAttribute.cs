using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.Attributes;
/// <summary>
/// Represents an attribute used to define dependency injection settings for a service.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DependencyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the lifetime of the service.
    /// </summary>
    /// <remarks>
    /// The <see cref="ServiceLifetime"/> determines how the service is instantiated and shared.
    /// </remarks>
    public ServiceLifetime? Lifetime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the service should only be registered if it is not already registered.
    /// </summary>
    /// <remarks>
    /// If set to <c>true</c>, the service will only be registered if it does not already exist in the service collection.
    /// </remarks>
    public bool TryRegister { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether existing services should be replaced during registration.
    /// </summary>
    /// <remarks>
    /// If set to <c>true</c>, any existing services of the same type will be replaced with the new registration.
    /// </remarks>
    public bool ReplaceServices { get; set; }
    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyAttribute"/> class.
    /// </summary>
    public DependencyAttribute()
    {
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyAttribute"/> class with the specified service lifetime.
    /// </summary>
    /// <param name="lifetime">The lifetime of the service.</param>
    public DependencyAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }
}
