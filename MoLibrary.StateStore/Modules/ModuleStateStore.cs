using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.MemoryProvider;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStore(ModuleStateStoreOption option)
    : MoModule<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.StateStore;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IMemoryStateStore, MemoryCacheProvider>();
        if (Option.UseDistributedProviderAsDefault)
        {
            
            services.AddSingleton<IMoStateStore>(serviceProvider =>
                serviceProvider.GetRequiredService<IDistributedStateStore>());
        }
        else
        {
            services.AddSingleton<IMoStateStore>(serviceProvider => serviceProvider.GetRequiredService<IMemoryStateStore>());
        }
    }
}