using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 异常池模块
/// 提供泛型异常池功能，用于收集和管理异常信息
/// </summary>
public class ModuleExceptionPool(ModuleExceptionPoolOption option)
    : MoModule<ModuleExceptionPool, ModuleExceptionPoolOption, ModuleExceptionPoolGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ExceptionPool;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册全局配置选项
        services.Configure<ExceptionPoolGlobalOption>(opt =>
        {
            opt.DefaultMaxSize = Option.GlobalOption.DefaultMaxSize;
            opt.EnableEventTrigger = Option.GlobalOption.EnableEventTrigger;
        });

        // 注册泛型异常池工厂
        services.AddSingleton(typeof(ExceptionPoolFactory<>));
    }
}
