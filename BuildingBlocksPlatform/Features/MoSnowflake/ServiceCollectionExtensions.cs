using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Features.MoSnowflake;

public static class ServiceCollectionExtensions
{
    public static void AddMoSnowflake(this IServiceCollection services, Action<SnowflakeConfiguration>? configAction = null)
    {
        var config = new SnowflakeConfiguration();
        configAction?.Invoke(config);
        var generator = new DefaultSingletonSnowflakeGenerator(config);
        services.AddSingleton<ISnowflakeGenerator>(generator);
    }
}