using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Abstractions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusGuide : MoModuleGuide<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>
{

    public ModuleEventBusGuide SetEventBusProvider<TProvider>() where TProvider : class, IMoDistributedEventBus
    {
        ConfigureServices(nameof(SetEventBusProvider), services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, TProvider>();
            services.Services.AddSingleton<TProvider>();
        });
        return this;
    }
}