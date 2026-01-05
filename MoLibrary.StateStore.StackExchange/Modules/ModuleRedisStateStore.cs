using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.StackExchange.Connection;

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
        // Only register connection factory (infrastructure)
        // IConnectionMultiplexer and IDistributedStateStore are registered in UseRedisStateStoreProvider
        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
    }
}
