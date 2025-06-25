using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoAmbientData;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// AmbientData模块扩展方法
/// </summary>
public static class ModuleAmbientDataBuilderExtensions
{
    /// <summary>
    /// 配置AmbientData模块
    /// </summary>
    /// <param name="builder">Web应用构建器</param>
    /// <param name="action">配置选项的操作</param>
    /// <returns>模块指导器</returns>
    public static ModuleAmbientDataGuide ConfigModuleAmbientData(this WebApplicationBuilder builder,
        Action<ModuleAmbientDataOption>? action = null)
    {
        return new ModuleAmbientDataGuide().Register(action);
    }
}

/// <summary>
/// AmbientData模块，用于在Scoped生命周期内管理环境数据
/// </summary>
public class ModuleAmbientData(ModuleAmbientDataOption option)
    : MoModule<ModuleAmbientData, ModuleAmbientDataOption, ModuleAmbientDataGuide>(option)
{
    /// <summary>
    /// 配置服务注册
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册AmbientData服务为Scoped生命周期
        services.AddScoped<IMoAmbientData, MoAmbientDataDefaultScopedProvider>();
        
        base.ConfigureServices(services);
    }

    /// <summary>
    /// 获取当前模块枚举
    /// </summary>
    /// <returns>模块枚举值</returns>
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.AmbientData;
    }
}

/// <summary>
/// AmbientData模块指导器
/// </summary>
public class ModuleAmbientDataGuide : MoModuleGuide<ModuleAmbientData, ModuleAmbientDataOption, ModuleAmbientDataGuide>
{
    // 可以在这里添加特定的配置方法
}

/// <summary>
/// AmbientData模块配置选项
/// </summary>
public class ModuleAmbientDataOption : MoModuleOption<ModuleAmbientData>
{
    // 可以在这里添加特定的配置选项
}
