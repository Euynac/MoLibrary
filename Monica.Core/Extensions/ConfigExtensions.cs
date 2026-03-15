using Microsoft.Extensions.DependencyInjection;

namespace Monica.Core.Extensions;

public static class ConfigExtensions
{
    /// <summary>
    /// Applies a configuration callback to a temporary options instance and registers it when provided.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configAction">The optional configuration callback.</param>
    public static void ConfigActionWrapper<TConfig>(this IServiceCollection services, Action<TConfig>? configAction)
        where TConfig : class, new()
    {
        services.ConfigActionWrapper(configAction, out _);
    }
    /// <summary>
    /// Applies a configuration callback to a temporary options instance, returns that instance,
    /// and registers the callback when provided.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configAction">The optional configuration callback.</param>
    /// <param name="tmpConfig">The temporary configuration instance populated by <paramref name="configAction"/>.</param>
    public static void ConfigActionWrapper<TConfig>(this IServiceCollection services, Action<TConfig>? configAction, out TConfig tmpConfig)
        where TConfig : class, new()
    {
        tmpConfig = new TConfig();
        configAction?.Invoke(tmpConfig);
        if (configAction != null)
        {
            services.Configure(configAction);
        }
    }
}
