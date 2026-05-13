using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Monica.Configuration.Projection;

/// <summary>
/// Microsoft configuration source that builds a Monica configuration provider from DI services.
/// </summary>
public sealed class MonicaConfigurationSource(IServiceProvider serviceProvider) : IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var provider = ActivatorUtilities.CreateInstance<MonicaConfigurationProvider>(serviceProvider);
        serviceProvider.GetRequiredService<MonicaConfigurationProviderAccessor>().Provider = provider;
        return provider;
    }
}
