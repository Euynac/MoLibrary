using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Attributes;

namespace Monica.DependencyInjection.AppInterfaces;

/// <summary>
/// Exposes a cached service provider for services activated by Monica DI.
/// </summary>
/// <remarks>
/// Monica populates this property only when the instance is created through its conventional
/// registration pipeline or dynamic-proxy activation pipeline.
/// Typical conventional registration entry points include <see cref="ITransientDependency"/>,
/// <see cref="IScopedDependency"/>, and
/// <see cref="DependencyAttribute"/>.
/// Manual <see cref="IServiceCollection"/> registrations and direct activation do not assign this
/// property automatically.
/// This interface must not be implemented by singleton services such as
/// <see cref="ISingletonDependency"/>.
/// </remarks>
public interface ICachedServiceProviderAccessor
{
    /// <summary>
    /// Gets or sets the cached service provider assigned during Monica-managed activation.
    /// </summary>
    ICachedServiceProvider CachedServiceProvider { get; set; }
}
