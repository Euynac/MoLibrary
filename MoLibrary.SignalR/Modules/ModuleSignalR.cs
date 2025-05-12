using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}