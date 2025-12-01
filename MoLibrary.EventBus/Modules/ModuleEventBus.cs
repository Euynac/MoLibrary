using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option) : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option), IWantIterateBusinessTypes
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBus;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoLocalEventBus, LocalEventBusProvider>();
        services.AddSingleton<LocalEventBusProvider>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
    }


    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if(Option.DisableAutoDiscovery) yield return type;
            if (type.IsImplementInterfaceGeneric(typeof(IMoDistributedEventHandler<>)))
            {
                Option.DistributedEventHandlers.Add(type);
            }
            if (type.IsImplementInterfaceGeneric(typeof(IMoLocalEventHandler<>)))
            {
                Option.LocalEventHandlers.Add(type);
            }
            yield return type;
        }
        
    }
}


