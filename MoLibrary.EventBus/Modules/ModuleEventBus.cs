using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Models;
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
            if (Option.DisableAutoDiscovery)
            {
                yield return type;
                continue;
            }

          
            if (type.IsImplementInterface<IMoEventHandler>())
            {
                try
                {
                    // Use factory method - handles all reflection and validation
                    var registrations = EventHandlerRegisterInfo.CreateFromHandlerType(type);
                    foreach (var registration in registrations)
                    {
                        Option.EventHandlers.Add(registration);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Fail fast with clear error at startup
                    throw new InvalidOperationException(
                        $"Failed to register event handler '{type.GetCleanFullName()}': {ex.Message}", ex);
                }
            }

            yield return type;
        }
    }
}


