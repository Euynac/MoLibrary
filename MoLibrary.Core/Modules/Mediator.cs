using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Modules;


public static class ModuleMediatorBuilderExtensions
{
    public static ModuleMediatorGuide ConfigModuleMediator(this WebApplicationBuilder builder,
        Action<ModuleMediatorOption>? action = null)
    {
        return new ModuleMediatorGuide().Register(action);
    }
}

public class ModuleMediator(ModuleMediatorOption option) : MoModule<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Mediator;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        //TODO 优化为使用统一迭代方法
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IServiceResponse>();
            cfg.RegisterServicesFromAssembly(Assembly.GetEntryAssembly()!);
        });
    }
}

public class ModuleMediatorGuide : MoModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>
{


}

public class ModuleMediatorOption : MoModuleOption<ModuleMediator>
{
}