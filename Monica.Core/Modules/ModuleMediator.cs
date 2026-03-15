using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleMediatorBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Mediator 模块
        /// </summary>
        public static ModuleMediatorGuide AddMediator(Action<ModuleMediatorOption>? action = null)
        {
            return new ModuleMediatorGuide().Register(action);
        }
    }
}

public class ModuleMediator(ModuleMediatorOption option) : MoModule<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Mediator;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        //TODO 优化为使用统一迭代方法
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IMoResponse>();
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