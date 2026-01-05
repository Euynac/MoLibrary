using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.StateStore.Modules;
using MoLibrary.StateStore.StackExchange.Connection;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange.Modules;

public static class ModuleRedisStateStoreBuilderExtensions
{
    /// <summary>
    /// Use Redis state store as the global distributed state store provider
    /// </summary>
    /// <param name="guide">StateStore module guide</param>
    /// <param name="action">Redis state store configuration delegate</param>
    /// <returns>Redis StateStore module guide instance for chaining</returns>
    public static ModuleRedisStateStoreGuide UseRedisStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleRedisStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<RedisStateStore>();

        // Register global IConnectionMultiplexer for common provider
        guide.ConfigureStateStoreServices(services =>
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                var options = sp.GetRequiredService<IOptions<ModuleRedisStateStoreOption>>().Value;
                return factory.CreateConnection(options);
            });
        });

        return new ModuleRedisStateStoreGuide().Register(action);
    }

    /// <summary>
    /// Add Redis state store as a keyed StateStore provider
    /// </summary>
    /// <param name="guide">StateStore module guide</param>
    /// <param name="serviceKey">Service key to identify this StateStore instance</param>
    /// <param name="configureOptions">Redis state store configuration delegate</param>
    /// <returns>StateStore module guide instance for chaining</returns>
    public static ModuleStateStoreGuide AddKeyedRedisStateStore(
        this ModuleStateStoreGuide guide,
        string serviceKey,
        Action<ModuleRedisStateStoreOption> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);
        
        new ModuleRedisStateStoreGuide().Register();
        return guide.ConfigureStateStoreServices(services =>
        {
            // Register keyed options
            services.Configure(serviceKey, configureOptions);

            // Register keyed IConnectionMultiplexer using factory
            services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
            {
                var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<ModuleRedisStateStoreOption>>();
                var keyedOptions = optionsSnapshot.Get(serviceKey);
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                return factory.CreateConnection(keyedOptions);
            });

            // Register keyed RedisStateStore
            services.AddKeyedSingleton<IMoStateStore>(serviceKey, (sp, _) =>
            {
                var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<ModuleRedisStateStoreOption>>();
                var keyedOptions = Options.Create(optionsSnapshot.Get(serviceKey));
                var keyedConnection = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
                return ActivatorUtilities.CreateInstance<RedisStateStore>(sp, keyedConnection, keyedOptions);
            });
        }, serviceKey);
    }
}
