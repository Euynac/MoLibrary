using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStore(ModuleStateStoreOption option)
    : MoModule<ModuleStateStore, ModuleStateStoreOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.StateStore;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}