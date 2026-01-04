using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.RegisterCentre.Modules;
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
        // 注册 Redis 连接
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModuleRedisStateStoreOption>>().Value;
            return ConnectionMultiplexer.Connect(options.ConnectionString);
        });

        // 注册 RedisStateStore 为 IDistributedStateStore
        services.AddSingleton<IDistributedStateStore, RedisStateStore>();
    }
}
