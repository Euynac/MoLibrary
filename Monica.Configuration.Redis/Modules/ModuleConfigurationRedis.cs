using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Redis.Notifications;
using Monica.Configuration.Redis.Sources;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for Redis integration in Monica.Configuration.
/// </summary>
public static class ModuleConfigurationRedisBuilderExtensions
{
    /// <summary>
    /// Registers Redis as a configuration value source.
    /// </summary>
    /// <param name="guide">The configuration guide.</param>
    /// <returns>The configuration guide.</returns>
    public static ModuleConfigurationGuide UseRedisConfigurationSource(this ModuleConfigurationGuide guide)
    {
        new ModuleConfigurationRedisGuide().Register();
        guide.AddValueSource<RedisConfigurationValueSource>();
        return guide;
    }

    /// <summary>
    /// Registers Redis as a change notification transport.
    /// </summary>
    /// <param name="guide">The configuration guide.</param>
    /// <returns>The configuration guide.</returns>
    public static ModuleConfigurationGuide UseRedisChangeNotifications(this ModuleConfigurationGuide guide)
    {
        new ModuleConfigurationRedisGuide().Register();
        return guide;
    }
}

/// <summary>
/// Redis provider module for Monica.Configuration.
/// </summary>
[ModuleKey("Monica.Configuration.Redis")]
public sealed class ModuleConfigurationRedis(ModuleConfigurationRedisOption option)
    : ModuleBase<ModuleConfigurationRedis, ModuleConfigurationRedisOption, ModuleConfigurationRedisGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigurationChangeBroadcaster, RedisConfigurationChangeBroadcaster>();
        services.AddSingleton<IConfigurationChangeSubscriber, RedisConfigurationChangeSubscriber>();
    }
}

/// <summary>
/// Fluent guide for Redis configuration integration.
/// </summary>
public sealed class ModuleConfigurationRedisGuide
    : ModuleGuide<ModuleConfigurationRedis, ModuleConfigurationRedisOption, ModuleConfigurationRedisGuide>;

/// <summary>
/// Module options for Redis configuration integration.
/// </summary>
public sealed class ModuleConfigurationRedisOption : ModuleOptions<ModuleConfigurationRedis>;
