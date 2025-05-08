using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : MoModule<ModuleDependencyInjection, ModuleDependencyInjectionOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DependencyInjection;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}