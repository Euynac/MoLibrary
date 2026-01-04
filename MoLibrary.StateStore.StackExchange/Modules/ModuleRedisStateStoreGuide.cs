using MoLibrary.Core.Module.Interfaces;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.StateStore.StackExchange.Modules;

public class ModuleRedisStateStoreGuide : MoModuleGuide<ModuleRedisStateStore, ModuleRedisStateStoreOption, ModuleRedisStateStoreGuide>
{
    public ModuleRedisStateStoreGuide()
    {
        // 依赖 StateStore 模块
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }
}
