using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

}