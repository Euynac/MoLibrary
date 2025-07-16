using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalROption : MoModuleControllerOption<ModuleSignalR>
{
    /// <summary>
    /// 注册的Hub类型
    /// </summary>
    internal List<Type> Hubs { get; set; } = [];
}