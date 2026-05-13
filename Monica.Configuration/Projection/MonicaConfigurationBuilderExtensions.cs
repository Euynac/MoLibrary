using Microsoft.Extensions.Configuration;

namespace Monica.Configuration.Projection;

/// <summary>
/// Extensions for adding Monica's merged configuration projection to Microsoft configuration builders.
/// </summary>
public static class MonicaConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the Monica configuration provider using services from an already-built service provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="serviceProvider">The service provider that owns Monica configuration services.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddMonicaConfiguration(
        this IConfigurationBuilder builder,
        IServiceProvider serviceProvider)
    {
        builder.Add(new MonicaConfigurationSource(serviceProvider));
        return builder;
    }
}
