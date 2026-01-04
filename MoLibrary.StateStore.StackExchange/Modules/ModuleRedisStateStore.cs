using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.StackExchange.Connection;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange.Modules;

public class ModuleRedisStateStore(ModuleRedisStateStoreOption option)
    : MoModule<ModuleRedisStateStore, ModuleRedisStateStoreOption, ModuleRedisStateStoreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RedisStateStore;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register connection factory
        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();

        // Register Redis connection using factory
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var factory = sp.GetRequiredService<IRedisConnectionFactory>();
            var options = sp.GetRequiredService<IOptions<ModuleRedisStateStoreOption>>().Value;
            return factory.CreateConnection(options);
        });

        // Register RedisStateStore as IDistributedStateStore
        services.AddSingleton<IDistributedStateStore, RedisStateStore>();
    }
}
