using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option) : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBus;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoLocalEventBus, LocalEventBus>();
        services.AddSingleton<LocalEventBus>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
    }
}


