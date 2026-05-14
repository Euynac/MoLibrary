using Microsoft.Extensions.Configuration;

namespace Monica.Configuration.Projection;

/// <summary>
/// Microsoft configuration source that builds a Monica configuration provider from DI services.
/// </summary>
internal sealed class MonicaConfigurationSource(MonicaConfigurationProviderAccessor accessor) : IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var provider = new MonicaConfigurationProvider(accessor);
        accessor.Provider = provider;
        return provider;
    }
}
