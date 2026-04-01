using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.DependencyInjection.Abstractions.Internal;

/// <summary>
/// Registers a discovered implementation type into an <see cref="IServiceCollection"/>.
/// </summary>
internal interface IConventionalRegistrar
{
    void AddType(IServiceCollection services, Type type);
}
