using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusOption : MoModuleControllerOption<ModuleEventBus>
{
    public string PubSubName { get; set; } = "pubsub";
    public string DaprEventBusCallback { get; set; } = "api/event-bus/dapr/event";
}
