using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Abstractions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusGuide : MoModuleGuide<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetDistributedEventBusProvider)];
    }

    public ModuleEventBusGuide SetDistributedEventBusProvider<TProvider>() where TProvider : class, IMoDistributedEventBus
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, TProvider>();
            services.Services.AddSingleton<TProvider>();
        });
        return this;
    }
}