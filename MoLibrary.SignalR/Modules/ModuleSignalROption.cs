using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalROption : MoModuleControllerOption<ModuleSignalR>
{
    public string ServerMethodsRoute { get; set; } = "/signalr/hubs";
}