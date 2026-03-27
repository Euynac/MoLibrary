using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ApiProjection;

namespace Monica.Core.Extensions;

public static class ExternalApiResultProjectionServiceCollectionExtensions
{
    public static IServiceCollection AddExternalApiResultProjection<TProjector>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProjector : class, IResultProjector
    {
        ArgumentNullException.ThrowIfNull(services);

        return lifetime switch
        {
            ServiceLifetime.Singleton => RegisterSingleton<TProjector>(services),
            ServiceLifetime.Scoped => RegisterScoped<TProjector>(services),
            ServiceLifetime.Transient => RegisterTransient<TProjector>(services),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
        };
    }

    private static IServiceCollection RegisterSingleton<TProjector>(IServiceCollection services)
        where TProjector : class, IResultProjector
    {
        services.AddSingleton<TProjector>();
        services.AddSingleton<IResultProjector>(serviceProvider => serviceProvider.GetRequiredService<TProjector>());
        return services;
    }

    private static IServiceCollection RegisterScoped<TProjector>(IServiceCollection services)
        where TProjector : class, IResultProjector
    {
        services.AddScoped<TProjector>();
        services.AddScoped<IResultProjector>(serviceProvider => serviceProvider.GetRequiredService<TProjector>());
        return services;
    }

    private static IServiceCollection RegisterTransient<TProjector>(IServiceCollection services)
        where TProjector : class, IResultProjector
    {
        services.AddTransient<TProjector>();
        services.AddTransient<IResultProjector>(serviceProvider => serviceProvider.GetRequiredService<TProjector>());
        return services;
    }
}
