using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprEventBusOption : MoModuleControllerOption<ModuleDaprEventBus>
{
    public string PubSubName { get; set; } = "pubsub";
    public string DaprEventBusCallback { get; set; } = "api/event-bus/dapr/event";
}