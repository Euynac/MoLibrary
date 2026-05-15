using Microsoft.Extensions.DependencyInjection;
using Monica.UnitTests.Modularity;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Builder for <see cref="MonicaTestHost"/>.
/// </summary>
public sealed class MonicaTestHostBuilder(System.Reflection.Assembly[] discoveryAssemblies)
{
    private readonly IServiceCollection _services = new ServiceCollection();

    /// <summary>
    /// Applies service registrations to the host.
    /// </summary>
    public MonicaTestHostBuilder WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_services);
        return this;
    }

    /// <summary>
    /// Builds the host.
    /// </summary>
    public MonicaTestHost Build()
    {
        return new MonicaTestHost(ModuleTestScope.Create(discoveryAssemblies), _services);
    }
}
