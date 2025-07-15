using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalROption : MoModuleControllerOption<ModuleSignalR>
{
    public string ServerMethodsRoute { get; set; } = "/signalr/hubs";

    /// <summary>
    /// 注册的Hub类型
    /// </summary>
    internal List<Type> Hubs { get; set; } = [];
}