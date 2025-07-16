using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.SignalR.Controllers;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalR(ModuleSignalROption option) : MoModuleWithDependencies<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }


    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleControllersGuide>().Register().RegisterMoControllers<ModuleSignalRController>(Option);
    }
}