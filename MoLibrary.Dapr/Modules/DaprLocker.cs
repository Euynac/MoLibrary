using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.Locker;
using MoLibrary.Locker.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprLockerBuilderExtensions
{
    public static ModuleDaprLockerGuide ConfigModuleDaprLocker(this WebApplicationBuilder builder,
        Action<ModuleDaprLockerOption>? action = null)
    {
        return new ModuleDaprLockerGuide().Register(action);
    }
}

public class ModuleDaprLocker(ModuleDaprLockerOption option)
    : MoModuleWithDependencies<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprLocker;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLockerGuide>().Register().SetDistributedLockProvider<DaprMoDistributedLock>();
    }
}

public class ModuleDaprLockerGuide : MoModuleGuide<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>
{
    

}

