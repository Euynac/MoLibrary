using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Redis.Notifications;
using Monica.Configuration.Redis.Sources;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.StateStore.StackExchange.Connection;

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
        services.TryAddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
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
public sealed class ModuleConfigurationRedisOption : ModuleOptions<ModuleConfigurationRedis>
{
    /// <summary>
    /// Gets Redis connection settings reused from the StackExchange Redis state store module.
    /// </summary>
    public ModuleRedisStateStoreOption Redis { get; } = new()
    {
        KeyPrefix = "monica:configuration"
    };

    /// <summary>
    /// Gets or sets the Redis key prefix used by Monica configuration override and notification keys.
    /// </summary>
    public string KeyPrefix { get; set; } = "monica:configuration";

    /// <summary>
    /// Gets or sets the pub/sub channel used for configuration change notifications.
    /// </summary>
    public string NotificationChannel { get; set; } = "monica:configuration:changes";

    /// <summary>
    /// Gets or sets the timeout for Redis source mutation locks.
    /// </summary>
    public TimeSpan MutationLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the retry interval used while waiting for a Redis source mutation lock.
    /// </summary>
    public TimeSpan MutationLockRetryInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Configures a normal single-node Redis connection.
    /// </summary>
    /// <param name="host">Redis host.</param>
    /// <param name="port">Redis port.</param>
    /// <param name="password">Optional Redis password.</param>
    /// <returns>The module options.</returns>
    public ModuleConfigurationRedisOption UseNormalConnection(
        string host = "localhost",
        int port = 6379,
        string? password = null)
    {
        Redis.UseNormalConnection(host, port, password);
        return this;
    }
}
