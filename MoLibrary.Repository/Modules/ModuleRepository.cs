using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;

namespace MoLibrary.Repository.Modules;

public class ModuleRepository(ModuleRepositoryOption option)
    : MoModuleWithDependencies<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Repository;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleMapperGuide>().Register();
    }
}