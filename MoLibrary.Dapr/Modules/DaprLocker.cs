using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.Locker;
using MoLibrary.Locker.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprLockerBuilderExtensions
{
    public static ModuleDaprLockerGuide UseDaprProvider(this ModuleLockerGuide guide,
        Action<ModuleDaprLockerOption>? action = null)
    {
        guide.SetDistributedLockProvider<DaprMoDistributedLock>();
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
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLockerGuide>().Register();
    }
}

public class ModuleDaprLockerGuide : MoModuleGuide<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>
{
    

}

public class ModuleDaprLockerOption : MoModuleOption<ModuleDaprLocker>
{
    public string StoreName { get; set; } = default!;

    public string? OwnerPrefix { get; set; }

    public TimeSpan DefaultExpirationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
