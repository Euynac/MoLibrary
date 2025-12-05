using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Abstractions.Handlers;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusOption : MoModuleControllerOption<ModuleEventBus>
{
    /// <summary>
    /// 是否禁止自动注册实现了 <see cref="IMoDistributedEventHandler{TEvent}"/>以及 <see cref="IMoLocalEventHandler{TEvent}"/> 的类型
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }
}
