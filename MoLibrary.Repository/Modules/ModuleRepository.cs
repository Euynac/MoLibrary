using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Repository.Modules;

public class ModuleRepository(ModuleRepositoryOption option)
    : MoModuleWithDependencies<ModuleRepository, ModuleRepositoryOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Repository;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleMapperGuide>().Register();
    }
}