using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option) : MoModule<ModuleEventBus, ModuleEventBusOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBus;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoLocalEventBus, LocalEventBus>();
        services.AddSingleton<LocalEventBus>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        return Res.Ok();
    }
}


