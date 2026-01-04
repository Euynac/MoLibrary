using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.StateStore.Modules;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange.Modules;

public static class ModuleRedisStateStoreBuilderExtensions
{
    /// <summary>
    /// 使用 Redis 状态存储作为全局分布式状态存储提供者
    /// </summary>
    /// <param name="guide">StateStore 模块指南</param>
    /// <param name="action">Redis 状态存储配置委托</param>
    /// <returns>Redis StateStore 模块指南实例以支持链式调用</returns>
    public static ModuleRedisStateStoreGuide UseRedisStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleRedisStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<RedisStateStore>();
        return new ModuleRedisStateStoreGuide().Register(action);
    }

    /// <summary>
    /// 添加 Redis 状态存储作为 Keyed StateStore 提供者
    /// </summary>
    /// <param name="guide">StateStore 模块指南</param>
    /// <param name="serviceKey">服务键，用于标识此 StateStore 实例</param>
    /// <param name="configureOptions">Redis 状态存储配置委托</param>
    /// <returns>StateStore 模块指南实例以支持链式调用</returns>
    public static ModuleStateStoreGuide AddKeyedRedisStateStore(
        this ModuleStateStoreGuide guide,
        string serviceKey,
        Action<ModuleRedisStateStoreOption> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return guide.ConfigureKeyedStateStore(services =>
        {
            // 注册 keyed options
            services.Configure(serviceKey, configureOptions);

            // 注册 keyed IConnectionMultiplexer
            services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
            {
                var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<ModuleRedisStateStoreOption>>();
                var keyedOptions = optionsSnapshot.Get(serviceKey);
                return ConnectionMultiplexer.Connect(keyedOptions.ConnectionString);
            });

            // 注册 keyed RedisStateStore
            services.AddKeyedSingleton<IMoStateStore>(serviceKey, (sp, _) =>
            {
                var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<ModuleRedisStateStoreOption>>();
                var keyedOptions = Options.Create(optionsSnapshot.Get(serviceKey));
                var keyedConnection = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
                return ActivatorUtilities.CreateInstance<RedisStateStore>(sp, keyedConnection, keyedOptions);
            });
        });
    }
}