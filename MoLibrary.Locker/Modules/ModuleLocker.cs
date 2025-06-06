using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Locker.Modules;

public class ModuleLocker(ModuleLockerOption option) : MoModule<ModuleLocker, ModuleLockerOption, ModuleLockerGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Locker;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDistributedLockKeyNormalizer, DistributedLockKeyNormalizer>();
        return Res.Ok();
    }
}