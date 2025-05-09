using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprBuilderExtensions
{
    public static ModuleDaprGuide AddMoModuleDapr(this IServiceCollection services,
        Action<ModuleDaprOption>? action = null)
    {
        return new ModuleDaprGuide().Register(action);
    }
}

public class ModuleDapr(ModuleDaprOption option) : MoModule<ModuleDapr, ModuleDaprOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Dapr;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
       
        return Res.Ok();
    }
}

public class ModuleDaprGuide : MoModuleGuide<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>
{

}

public class ModuleDaprOption : IMoModuleOption<ModuleDapr>
{
    
}
